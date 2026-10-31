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
            public PendingVolley(Squad shooter, Squad target, int shooters, float distance, int casualties, bool pointBlank, int misfires)
            {
                Misfires = misfires;
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

            public int Misfires { get; }
        }

        private static readonly List<PendingVolley> Pending = new List<PendingVolley>();

        public static void Update(Match match, float deltaTime, List<VolleyEvent> events)
        {
            CombatSettings combat = match.Settings.combat;
            Pending.Clear();

            foreach (Squad squad in match.Squads)
            {
                // Пушки заряжает и наводит ArtillerySystem.
                if (squad.IsArtillery)
                {
                    continue;
                }

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

            int ready = VolleyMath.Shooters(shooter.Formation, shooter.Alive, match.Settings.squads);
            // Местность у цели: лес укрывает, брод открывает.
            float cover = match.Terrain != null ? match.Terrain.TargetHitMultiplier(target.Position, match.Settings.terrain) : 1f;
            // Дальнобойность (рода войск и холма) растягивает всю кривую попадания: при множителе 1.25 стрелок на 1.25 × d бьёт, как обычный на d.
            UnitTypeSettings unit = UnitStats.Of(shooter, match.Settings);
            float scaledDistance = distance / UnitStats.RangeMultiplier(shooter, match.Settings);
            cover *= UnitStats.Of(target, match.Settings).targetHitMultiplier;
            int shooters;
            int casualties;
            if (shooter.Formation == Formation.Column || shooter.FileLimit > 0)
            {
                casualties = DeepVolley(match, shooter, target, distance, cover, unit, out shooters);
            }
            else
            {
                // В дождь сырой порох: часть ружей даёт осечку и не стреляет.
                shooters = WeatherSystem.RollFired(match, ready);
                float probability = VolleyMath.HitProbability(scaledDistance, shooter.IsFirstVolley, target.Formation, combat, cover, unit.accuracyMultiplier);
                casualties = VolleyMath.RollCasualties(match.Random, shooters, probability, target.Alive);
            }

            bool pointBlank = scaledDistance <= combat.pointBlankRange;
            Pending.Add(new PendingVolley(shooter, target, shooters, distance, casualties, pointBlank, ready - shooters));
        }

        /// <summary>
        /// Залп глубоким строем (колонна или строй, сжатый на мосту): стреляют все шеренги, но каждая со своей
        /// дистанции — задние дальше от врага и бьют хуже; шеренги дальше обычных двух линейных стреляют через головы
        /// своих — меткость × columnVolleyAccuracyMultiplier. Осечки в дождь — по шеренгам.
        /// </summary>
        private static int DeepVolley(Match match, Squad shooter, Squad target, float frontDistance, float cover, UnitTypeSettings unit, out int fired)
        {
            SimulationSettings settings = match.Settings;
            FormationLayout.GetGrid(shooter, settings.squads, out int files, out int ranks);
            float rangeMultiplier = UnitStats.RangeMultiplier(shooter, settings);
            fired = 0;
            int casualties = 0;
            for (int rank = 0; rank < ranks; rank++)
            {
                int inRank = System.Math.Min(files, shooter.Alive - rank * files);
                int rankFired = WeatherSystem.RollFired(match, inRank);
                fired += rankFired;
                float rankDistance = VolleyMath.ColumnRankDistance(frontDistance, rank, settings.squads) / rangeMultiplier;
                float accuracy = unit.accuracyMultiplier * (rank >= settings.squads.lineRanks ? settings.combat.columnVolleyAccuracyMultiplier : 1f);
                float probability = VolleyMath.HitProbability(rankDistance, shooter.IsFirstVolley, target.Formation, settings.combat, cover, accuracy);
                casualties += VolleyMath.RollCasualties(match.Random, rankFired, probability, target.Alive - casualties);
            }

            return casualties;
        }

        /// <summary>
        /// Стрельба вразнобой: каждый стреляет сам, когда враг в эффективной дальности, раз в reloadTime.
        /// Выстрел сначала готовится: человек целится aimTime секунд (это видно), затем стреляет по ближайшему врагу
        /// в эффективной дальности; если врага уже нет — выстрел пропадает. Попадание — с вероятностью залпа на этой
        /// дистанции (с меткостью рода войск).
        /// </summary>
        private static void SkirmishFire(Match match, Squad squad, float deltaTime, List<VolleyEvent> events)
        {
            CombatSettings combat = match.Settings.combat;
            float range = UnitStats.EffectiveRange(squad, match.Settings);
            FireReadyAims(match, squad, deltaTime, range, events);

            Squad target = Targeting.FindTarget(match, squad, range);
            if (target == null || squad.Alive <= 0 || combat.reloadTime <= 0f)
            {
                squad.SkirmishShots = 0f;
                return;
            }

            squad.SkirmishShots += squad.Alive / combat.reloadTime * deltaTime;
            while (squad.SkirmishShots >= 1f)
            {
                squad.SkirmishShots -= 1f;
                squad.SkirmishAimList.Add(new SkirmishAim(squad.SkirmishShotCount++, combat.aimTime));
            }
        }

        private static void FireReadyAims(Match match, Squad squad, float deltaTime, float range, List<VolleyEvent> events)
        {
            List<SkirmishAim> aims = squad.SkirmishAimList;
            for (int i = 0; i < aims.Count; i++)
            {
                SkirmishAim aim = aims[i];
                float remaining = aim.Remaining - deltaTime;
                if (remaining > TimerEpsilon)
                {
                    aims[i] = new SkirmishAim(aim.ShooterIndex, remaining);
                    continue;
                }

                aims.RemoveAt(i);
                i--;
                Squad target = squad.Alive > 0 ? Targeting.FindTarget(match, squad, range) : null;
                if (target != null && target.Alive > 0)
                {
                    FireSingleShot(match, squad, target, aim.ShooterIndex, events);
                }
            }
        }

        private static void FireSingleShot(Match match, Squad squad, Squad target, int shooterIndex, List<VolleyEvent> events)
        {
            CombatSettings combat = match.Settings.combat;
            UnitTypeSettings unit = UnitStats.Of(squad, match.Settings);
            float distance = FireGeometry.Distance(squad, target, match.Settings.squads);
            if (WeatherSystem.RollFired(match, 1) == 0)
            {
                // Осечка: порох отсырел, выстрела нет.
                events.Add(new VolleyEvent(
                    match.CurrentTick, squad.Id, squad.Side, 0, distance, 0,
                    target.Id, target.Alive, target.Position, target.Facing, target.Formation, shooterIndex, 1));
                return;
            }

            float scaledDistance = distance / UnitStats.RangeMultiplier(squad, match.Settings);
            float cover = (match.Terrain != null ? match.Terrain.TargetHitMultiplier(target.Position, match.Settings.terrain) : 1f)
                * UnitStats.Of(target, match.Settings).targetHitMultiplier;
            float probability = VolleyMath.HitProbability(scaledDistance, false, target.Formation, combat, cover, unit.accuracyMultiplier);
            int aliveBefore = target.Alive;
            int casualties = match.Random.NextFloat() < probability ? 1 : 0;
            target.Alive -= casualties;
            bool flank = MoraleMath.IsFlank(target, squad.Position, match.Settings.morale);
            MoraleMath.ApplyLoss(target, casualties, aliveBefore, scaledDistance <= combat.pointBlankRange, flank, match.Settings);
            MoraleMath.ApplyKills(squad, casualties, match.Settings);
            events.Add(new VolleyEvent(
                match.CurrentTick, squad.Id, squad.Side, 1, distance, casualties,
                target.Id, aliveBefore, target.Position, target.Facing, target.Formation, shooterIndex));
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
            // Удачный залп ободряет стрелков.
            MoraleMath.ApplyKills(shooter, casualties, match.Settings);

            events.Add(new VolleyEvent(
                match.CurrentTick, shooter.Id, shooter.Side, volley.Shooters, volley.Distance, casualties,
                target.Id, aliveBefore, target.Position, target.Facing, target.Formation, -1, volley.Misfires));
        }
    }
}
