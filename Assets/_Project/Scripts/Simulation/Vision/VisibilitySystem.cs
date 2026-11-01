using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Vision
{
    /// <summary>
    /// Туман войны: в конце тика решает, какие вражеские отряды видит каждая сторона — те, что ближе радиуса обзора
    /// хотя бы одного её отряда, здания (в том числе строящегося) или её деревни. Сторона с воздушным шаром видит всё.
    /// Здесь же идёт изучение шара в штабе.
    /// </summary>
    public static class VisibilitySystem
    {
        // Таймеры складываются из шагов 0.1 с во float; допуск не даёт лишнего тика из-за ошибки округления.
        private const float TimerEpsilon = 1e-4f;

        /// <summary>Радиус обзора: дальность выстрела гренадёров × visionRangeMultiplier, u.</summary>
        public static float Range(SimulationSettings settings)
        {
            return settings.combat.maxRange * settings.grenadiers.rangeMultiplier * settings.vision.visionRangeMultiplier;
        }

        internal static void Update(Match match, float deltaTime)
        {
            foreach (PlayerState player in match.Players)
            {
                if (player.BalloonResearchRemaining <= 0f)
                {
                    continue;
                }

                player.BalloonResearchRemaining -= deltaTime;
                if (player.BalloonResearchRemaining <= TimerEpsilon)
                {
                    player.BalloonResearchRemaining = 0f;
                    player.HasBalloon = true;
                }
            }

            Refresh(match);
        }

        /// <summary>Пересчитывает, кто кого видит (без хода времени) — при включении тумана и в конце каждого тика.</summary>
        internal static void Refresh(Match match)
        {
            foreach (Squad squad in match.Squads)
            {
                for (int side = 0; side < 2; side++)
                {
                    var viewer = (Side)side;
                    squad.SeenBy[side] = viewer == squad.Side || IsPointSeen(match, squad.Position, viewer);
                }
            }
        }

        /// <summary>Видит ли сторона точку: шар изучен или точка в обзоре её отряда, здания или деревни.</summary>
        public static bool IsPointSeen(Match match, float2 point, Side viewer)
        {
            if (match.GetPlayer(viewer).HasBalloon)
            {
                return true;
            }

            float range = Range(match.Settings);
            foreach (Squad own in match.Squads)
            {
                if (own.Side == viewer && own.Alive > 0 && !own.IsWithdrawn && Within(own.Position, point, range))
                {
                    return true;
                }
            }

            foreach (Building building in match.Buildings)
            {
                if (building.Owner == viewer && !building.IsDestroyed && Within(building.Position, point, range))
                {
                    return true;
                }
            }

            foreach (Village village in match.Villages)
            {
                if (village.Owner == viewer && Within(village.Position, point, range))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Within(float2 a, float2 b, float range)
        {
            return range > 0f && math.distancesq(a, b) <= range * range;
        }
    }
}
