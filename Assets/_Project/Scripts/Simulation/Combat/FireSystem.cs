using System.Collections.Generic;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Morale;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Combat
{
    /// <summary>
    /// Автомат стрельбы всех отрядов. Залпы тика рассчитываются по численности на начало тика и применяются
    /// после расчёта, поэтому взаимный залп не зависит от порядка отрядов в списке.
    /// </summary>
    internal static class FireSystem
    {
        // Таймеры складываются из шагов 0.1 с во float; допуск не даёт лишнего тика из-за ошибки округления.
        private const float TimerEpsilon = 1e-4f;

        private readonly struct PendingVolley
        {
            public PendingVolley(Squad shooter, Squad target, int shooters, float distance, int casualties, bool pointBlank)
            {
                Shooter = shooter;
                Target = target;
                Shooters = shooters;
                Distance = distance;
                Casualties = casualties;
                PointBlank = pointBlank;
            }

            public Squad Shooter { get; }

            public Squad Target { get; }

            public int Shooters { get; }

            public float Distance { get; }

            public int Casualties { get; }

            public bool PointBlank { get; }
        }

        private static readonly List<PendingVolley> Pending = new List<PendingVolley>();

        public static void Update(Match match, float deltaTime, List<VolleyEvent> events)
        {
            CombatSettings combat = match.Settings.combat;
            Pending.Clear();

            foreach (Squad squad in match.Squads)
            {
                if (squad.FireState == FireState.Reloading)
                {
                    squad.FireTimer -= deltaTime;
                    if (squad.FireTimer <= TimerEpsilon)
                    {
                        squad.FireTimer = 0f;
                        squad.FireState = FireState.Loaded;
                    }

                    continue;
                }

                if (squad.FireState == FireState.Aiming)
                {
                    squad.FireTimer -= deltaTime;
                    if (squad.FireTimer <= TimerEpsilon)
                    {
                        PrepareVolley(match, squad);
                    }
                }
            }

            foreach (PendingVolley volley in Pending)
            {
                ApplyVolley(match, volley, events);
            }

            foreach (Squad squad in match.Squads)
            {
                if (UnitStats.Of(squad, match.Settings).skirmishFire)
                {
                    SkirmishFire(match, squad, deltaTime, events);
                }
            }

            Pending.Clear();
        }

        public static void StartAiming(Squad squad, Squad target, CombatSettings combat, float maxDistance = float.MaxValue)
        {
            squad.FireState = FireState.Aiming;
            squad.FireTimer = combat.aimTime;
            squad.AimTargetId = target.Id;
            squad.AimMaxDistance = maxDistance;
        }

        public static void CancelAiming(Squad squad)
        {
            if (squad.FireState != FireState.Aiming)
            {
                return;
            }

            squad.FireState = FireState.Loaded;
            squad.FireTimer = 0f;
            squad.AimTargetId = 0;
            squad.AimMaxDistance = float.MaxValue;
        }

        private static void PrepareVolley(Match match, Squad shooter)
        {
            CombatSettings combat = match.Settings.combat;
            float range = System.Math.Min(UnitStats.MaxRange(shooter, match.Settings), shooter.AimMaxDistance);
            Squad target = match.FindSquad(shooter.AimTargetId);
            if (!Targeting.IsValidTarget(match, shooter, target, range, out float distance))
            {
                target = Targeting.FindTarget(match, shooter, range);
                if (target == null)
                {
                    CancelAiming(shooter);
                    return;
                }

                distance = FireGeometry.Distance(shooter, target, match.Settings.squads);
            }

            int shooters = VolleyMath.Shooters(shooter.Formation, shooter.Alive, match.Settings.squads);
            bool inCover = match.Terrain != null && match.Terrain.IsInForest(target.Position);
            float cover = inCover ? match.Settings.terrain.forestCoverMultiplier : 1f;
            // Дальнобойность рода войск растягивает всю кривую попадания: при множителе 1.25 стрелок на 1.25 × d бьёт, как обычный на d.
            UnitTypeSettings unit = UnitStats.Of(shooter, match.Settings);
            float scaledDistance = distance / unit.rangeMultiplier;
            cover *= UnitStats.Of(target, match.Settings).targetHitMultiplier;
            float probability = VolleyMath.HitProbability(scaledDistance, shooter.IsFirstVolley, target.Formation, combat, cover, unit.accuracyMultiplier);
            int casualties = VolleyMath.RollCasualties(match.Random, shooters, probability, target.Alive);
            bool pointBlank = scaledDistance <= combat.pointBlankRange;
            Pending.Add(new PendingVolley(shooter, target, shooters, distance, casualties, pointBlank));
        }

        /// <summary>
        /// Стрельба вразнобой: каждый стреляет сам, когда враг в эффективной дальности, раз в reloadTime.
        /// Выстрелы тика — по одному, каждый попадает с вероятностью залпа на этой дистанции (с меткостью рода войск).
        /// </summary>
        private static void SkirmishFire(Match match, Squad squad, float deltaTime, List<VolleyEvent> events)
        {
            CombatSettings combat = match.Settings.combat;
            Squad target = Targeting.FindTarget(match, squad, UnitStats.EffectiveRange(squad, match.Settings));
            if (target == null || squad.Alive <= 0 || combat.reloadTime <= 0f)
            {
                squad.SkirmishShots = 0f;
                return;
            }

            squad.SkirmishShots += squad.Alive / combat.reloadTime * deltaTime;
            UnitTypeSettings unit = UnitStats.Of(squad, match.Settings);
            float distance = FireGeometry.Distance(squad, target, match.Settings.squads);
            float scaledDistance = distance / unit.rangeMultiplier;
            float cover = (match.Terrain != null && match.Terrain.IsInForest(target.Position) ? match.Settings.terrain.forestCoverMultiplier : 1f)
                * UnitStats.Of(target, match.Settings).targetHitMultiplier;
            float probability = VolleyMath.HitProbability(scaledDistance, false, target.Formation, combat, cover, unit.accuracyMultiplier);
            while (squad.SkirmishShots >= 1f && target.Alive > 0)
            {
                squad.SkirmishShots -= 1f;
                int aliveBefore = target.Alive;
                int casualties = match.Random.NextFloat() < probability ? 1 : 0;
                target.Alive -= casualties;
                bool flank = MoraleMath.IsFlank(target, squad.Position, match.Settings.morale);
                MoraleMath.ApplyLoss(target, casualties, aliveBefore, scaledDistance <= combat.pointBlankRange, flank, match.Settings);
                events.Add(new VolleyEvent(
                    match.CurrentTick, squad.Id, squad.Side, 1, distance, casualties,
                    target.Id, aliveBefore, target.Position, target.Facing, target.Formation));
            }
        }

        private static void ApplyVolley(Match match, PendingVolley volley, List<VolleyEvent> events)
        {
            Squad shooter = volley.Shooter;
            Squad target = volley.Target;
            int aliveBefore = target.Alive;
            // Если по цели в этот же тик били несколько отрядов, убить больше, чем осталось, нельзя.
            int casualties = System.Math.Min(volley.Casualties, aliveBefore);
            target.Alive = aliveBefore - casualties;

            shooter.FireState = FireState.Reloading;
            shooter.FireTimer = match.Settings.combat.reloadTime;
            shooter.IsFirstVolley = false;
            shooter.AimTargetId = 0;
            shooter.AimMaxDistance = float.MaxValue;

            bool flank = MoraleMath.IsFlank(target, shooter.Position, match.Settings.morale);
            MoraleMath.ApplyLoss(target, casualties, aliveBefore, volley.PointBlank, flank, match.Settings);

            events.Add(new VolleyEvent(
                match.CurrentTick, shooter.Id, shooter.Side, volley.Shooters, volley.Distance, casualties,
                target.Id, aliveBefore, target.Position, target.Facing, target.Formation));
        }
    }
}
