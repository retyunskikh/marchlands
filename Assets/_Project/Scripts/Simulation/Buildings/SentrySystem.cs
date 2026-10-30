using System.Collections.Generic;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Morale;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Buildings
{
    /// <summary>
    /// Часовые построенных зданий: раз в sentryInterval секунд — маленький залп из sentryShots ружей по ближайшему
    /// не бегущему врагу в sentryRange. Попадание — sentryHitChance с множителями колонны, леса и рода войск цели;
    /// потери снимают мораль, как обычный залп. Пока стрелять не по кому, часовые наготове и бьют сразу.
    /// </summary>
    internal static class SentrySystem
    {
        // Таймеры складываются из шагов 0.1 с во float; допуск не даёт лишнего тика из-за ошибки округления.
        private const float TimerEpsilon = 1e-4f;

        public static void Update(Match match, float deltaTime, List<SentryVolley> events)
        {
            BuildingSettings settings = match.Settings.buildings;
            foreach (Building building in match.Buildings)
            {
                if (!building.IsBuilt || building.IsDestroyed || settings.sentryShots <= 0)
                {
                    continue;
                }

                building.SentryTimer = math.max(0f, building.SentryTimer - deltaTime);
                if (building.SentryTimer > TimerEpsilon)
                {
                    continue;
                }

                Squad target = FindTarget(match, building, settings.sentryRange);
                if (target != null)
                {
                    Fire(match, building, target, events);
                    building.SentryTimer = settings.sentryInterval;
                }
            }
        }

        private static Squad FindTarget(Match match, Building building, float range)
        {
            Squad best = null;
            float bestDistance = range * range;
            foreach (Squad squad in match.Squads)
            {
                if (squad.Side == building.Owner || squad.Alive <= 0 || squad.IsRouting || squad.IsWithdrawn)
                {
                    continue;
                }

                float distance = math.distancesq(squad.Position, building.Position);
                if (distance <= bestDistance)
                {
                    best = squad;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static void Fire(Match match, Building building, Squad target, List<SentryVolley> events)
        {
            SimulationSettings settings = match.Settings;
            float probability = settings.buildings.sentryHitChance * UnitStats.Of(target, settings).targetHitMultiplier;
            if (target.Formation == Formation.Column)
            {
                probability *= settings.combat.columnTargetMultiplier;
            }

            if (match.Terrain != null && match.Terrain.IsInForest(target.Position))
            {
                probability *= settings.terrain.forestCoverMultiplier;
            }

            int shooters = settings.buildings.sentryShots;
            int aliveBefore = target.Alive;
            int casualties = VolleyMath.RollCasualties(match.Random, shooters, math.saturate(probability), aliveBefore);
            target.Alive -= casualties;
            bool flank = MoraleMath.IsFlank(target, building.Position, settings.morale);
            MoraleMath.ApplyLoss(target, casualties, aliveBefore, false, flank, settings);
            events.Add(new SentryVolley(building.Id, building.Owner, building.Position, shooters, casualties, target.Id, target.Position));
        }
    }
}
