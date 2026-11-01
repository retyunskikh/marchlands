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
    /// хотя бы одного её отряда, построенного здания или её деревни. Сторона с воздушным шаром видит всех.
    /// Здесь же идёт изучение шара в штабе.
    /// </summary>
    internal static class VisibilitySystem
    {
        // Таймеры складываются из шагов 0.1 с во float; допуск не даёт лишнего тика из-за ошибки округления.
        private const float TimerEpsilon = 1e-4f;

        public static void Update(Match match, float deltaTime)
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
        public static void Refresh(Match match)
        {
            foreach (Squad squad in match.Squads)
            {
                for (int side = 0; side < 2; side++)
                {
                    var viewer = (Side)side;
                    squad.SeenBy[side] = viewer == squad.Side || match.GetPlayer(viewer).HasBalloon || IsSeen(match, squad, viewer);
                }
            }
        }

        private static bool IsSeen(Match match, Squad target, Side viewer)
        {
            VisionSettings vision = match.Settings.vision;
            foreach (Squad own in match.Squads)
            {
                if (own.Side == viewer && own.Alive > 0 && !own.IsWithdrawn && Within(own.Position, target.Position, RangeOf(own, vision)))
                {
                    return true;
                }
            }

            foreach (Building building in match.Buildings)
            {
                if (building.Owner == viewer && building.IsBuilt && !building.IsDestroyed
                    && Within(building.Position, target.Position, vision.buildingVisionRange))
                {
                    return true;
                }
            }

            foreach (Village village in match.Villages)
            {
                if (village.Owner == viewer && Within(village.Position, target.Position, vision.villageVisionRange))
                {
                    return true;
                }
            }

            return false;
        }

        private static float RangeOf(Squad squad, VisionSettings vision)
        {
            return squad.Type == UnitType.Cavalry ? vision.cavalryVisionRange
                : squad.IsArtillery ? vision.artilleryVisionRange
                : vision.infantryVisionRange;
        }

        private static bool Within(float2 a, float2 b, float range)
        {
            return range > 0f && math.distancesq(a, b) <= range * range;
        }
    }
}
