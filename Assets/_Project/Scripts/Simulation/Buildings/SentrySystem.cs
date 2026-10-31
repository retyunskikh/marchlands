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
    /// не бегущему врагу в sentryRange. Блокгауз стреляет так же, но своими, более сильными параметрами (blockhouse*). Попадание — sentryHitChance с множителями колонны, леса и рода войск цели;
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
                Volley volley = GetVolley(building, settings);
                if (!building.IsBuilt || building.IsDestroyed || volley.Shots <= 0)
                {
                    continue;
                }

                building.SentryTimer = math.max(0f, building.SentryTimer - deltaTime);
                float aimTime = math.min(match.Settings.combat.aimTime, volley.Interval);
                if (building.IsSentryAiming)
                {
                    building.SentryAimRemaining -= deltaTime;
                    if (building.SentryAimRemaining > TimerEpsilon)
                    {
                        continue;
                    }

                    // Залп — по той же цели, если она ещё в дальности, иначе по ближайшей; нет никого — часовые снова наготове.
                    Squad aimed = match.FindSquad(building.SentryAimTargetId);
                    Squad target = IsValidTarget(building, aimed, volley.Range) ? aimed : FindTarget(match, building, volley.Range);
                    building.SentryAimTargetId = 0;
                    building.SentryAimRemaining = 0f;
                    if (target != null)
                    {
                        Fire(match, building, target, volley, events);
                        // Прицеливание входит в интервал: при непрерывном огне залп идёт ровно раз в interval.
                        building.SentryTimer = volley.Interval - aimTime;
                    }

                    continue;
                }

                if (building.SentryTimer > TimerEpsilon)
                {
                    continue;
                }

                // Часовые вскидывают ружья и целятся aimTime секунд — так видно, откуда сейчас ударят.
                Squad found = FindTarget(match, building, volley.Range);
                if (found != null)
                {
                    building.SentryAimTargetId = found.Id;
                    building.SentryAimRemaining = aimTime;
                }
            }
        }

        private static Volley GetVolley(Building building, BuildingSettings settings)
        {
            return Volley.Of(building, settings);
        }

        private static bool IsValidTarget(Building building, Squad squad, float range)
        {
            return squad != null && squad.Side != building.Owner && squad.Alive > 0 && !squad.IsRouting && !squad.IsWithdrawn
                && math.distancesq(squad.Position, building.Position) <= range * range;
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

        private static void Fire(Match match, Building building, Squad target, Volley volley, List<SentryVolley> events)
        {
            SimulationSettings settings = match.Settings;
            float probability = volley.HitChance * UnitStats.Of(target, settings).targetHitMultiplier;
            if (target.Formation == Formation.Column)
            {
                probability *= settings.combat.columnTargetMultiplier;
            }

            if (match.Terrain != null && match.Terrain.IsInForest(target.Position))
            {
                probability *= settings.terrain.forestCoverMultiplier;
            }

            int shooters = WeatherSystem.RollFired(match, volley.Shots);
            int aliveBefore = target.Alive;
            int casualties = VolleyMath.RollCasualties(match.Random, shooters, math.saturate(probability), aliveBefore);
            target.Alive -= casualties;
            bool flank = MoraleMath.IsFlank(target, building.Position, settings.morale);
            MoraleMath.ApplyLoss(target, casualties, aliveBefore, false, flank, settings);
            events.Add(new SentryVolley(building.Id, building.Owner, building.Position, shooters, casualties, target.Id, target.Position,
                volley.Shots - shooters));
        }
    }
}
