using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Buildings
{
    /// <summary>
    /// Постройка зданий и найм. Казарма с включённым наймом раз в hireInterval за hireCost получает рекрута
    /// в резерв стороны, конюшня — всадника в резерв конницы. Найм ждёт, если не хватает золота, если у зданий
    /// ждёт предельное число людей или если их уже хватает на отряд цели найма.
    /// </summary>
    internal static class BuildingSystem
    {
        // Таймеры складываются из шагов 0.1 с во float; допуск не даёт лишнего тика из-за ошибки округления.
        private const float TimerEpsilon = 1e-4f;

        public static void Update(Match match, float deltaTime)
        {
            BuildingSettings settings = match.Settings.buildings;

            for (int i = 0; i < match.Buildings.Count; i++)
            {
                Building building = match.Buildings[i];
                if (!building.IsBuilt)
                {
                    building.BuildRemaining -= deltaTime;
                    if (building.BuildRemaining <= TimerEpsilon)
                    {
                        building.BuildRemaining = 0f;
                    }

                    continue;
                }

                if (building.IsHiring)
                {
                    Hire(match, building, settings, deltaTime);
                }
            }
        }

        /// <summary>Рекрут приходит, когда истёк таймер и найм не ждёт (см. <see cref="BuildRules.IsWaiting"/>).</summary>
        private static void Hire(Match match, Building building, BuildingSettings settings, float deltaTime)
        {
            building.HireRemaining = math.max(0f, building.HireRemaining - deltaTime);
            if (building.HireRemaining > TimerEpsilon || BuildRules.IsWaiting(match, building) != HireWait.None)
            {
                return;
            }

            bool stable = building.Type == BuildingType.Stable;
            PlayerState player = match.GetPlayer(building.Owner);
            player.Gold -= stable ? settings.cavalryHireCost : settings.hireCost;
            player.AddReserve(stable ? UnitType.Cavalry : UnitType.Grenadiers, 1);
            building.HireRemaining = BuildRules.GetHireInterval(building, settings);
        }

        /// <summary>Отряд появляется в spawnOffset от здания со стороны вражеского штаба, фронтом к нему.</summary>
        internal static Squad SpawnSquad(Match match, Building barracks, UnitType type)
        {
            float2 direction = GetFrontDirection(match, barracks);
            float2 position = barracks.Position + direction * match.Settings.buildings.spawnOffset;
            return match.AddSquad(barracks.Owner, position, Angles.Of(direction), Formation.Line, type);
        }

        private static float2 GetFrontDirection(Match match, Building barracks)
        {
            Side enemy = barracks.Owner == Side.France ? Side.Austria : Side.France;
            Building enemyHeadquarters = match.GetHeadquarters(enemy);
            if (enemyHeadquarters != null && math.distancesq(enemyHeadquarters.Position, barracks.Position) > 0f)
            {
                return math.normalize(enemyHeadquarters.Position - barracks.Position);
            }

            // Без вражеского штаба — прочь от своего.
            Building ownHeadquarters = match.GetHeadquarters(barracks.Owner);
            if (ownHeadquarters != null && math.distancesq(ownHeadquarters.Position, barracks.Position) > 0f)
            {
                return math.normalize(barracks.Position - ownHeadquarters.Position);
            }

            return Angles.Direction(barracks.Owner == Side.France ? 90f : -90f);
        }
    }
}
