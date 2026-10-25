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
            public PendingVolley(Squad shooter, Squad target, int shooters, float distance, int casualties)
            {
                Shooter = shooter;
                Target = target;
                Shooters = shooters;
                Distance = distance;
                Casualties = casualties;
            }

            public Squad Shooter { get; }

            public Squad Target { get; }

            public int Shooters { get; }

            public float Distance { get; }

            public int Casualties { get; }
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

                if (squad.FireState == FireState.Loaded && squad.FireAtWill && VolleyRules.CanStartAiming(squad))
                {
                    Squad target = Targeting.FindTarget(match, squad, combat.effectiveRange);
                    if (target != null)
                    {
                        StartAiming(squad, target, combat);
                    }
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
            float range = System.Math.Min(combat.maxRange, shooter.AimMaxDistance);
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
            float probability = VolleyMath.HitProbability(distance, shooter.IsFirstVolley, target.Formation, combat);
            int casualties = VolleyMath.RollCasualties(match.Random, shooters, probability, target.Alive);
            Pending.Add(new PendingVolley(shooter, target, shooters, distance, casualties));
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

            bool pointBlank = volley.Distance <= match.Settings.combat.pointBlankRange;
            bool flank = MoraleMath.IsFlank(target, shooter.Position, match.Settings.morale);
            MoraleMath.ApplyLoss(target, casualties, aliveBefore, pointBlank, flank, match.Settings.morale);

            events.Add(new VolleyEvent(
                match.CurrentTick, shooter.Id, shooter.Side, volley.Shooters, volley.Distance, casualties,
                target.Id, aliveBefore, target.Position, target.Facing, target.Formation));
        }
    }
}
