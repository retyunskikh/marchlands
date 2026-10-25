using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Buildings
{
    /// <summary>Постройка казарм и найм: отряды очереди нанимаются по одному и появляются рядом с казармой.</summary>
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

                if (building.RecruitQueue <= 0)
                {
                    continue;
                }

                building.RecruitRemaining -= deltaTime;
                if (building.RecruitRemaining > TimerEpsilon)
                {
                    continue;
                }

                SpawnSquad(match, building, settings);
                building.RecruitQueue--;
                building.RecruitRemaining = building.RecruitQueue > 0 ? settings.recruitTime : 0f;
            }
        }

        /// <summary>Отряд появляется в spawnOffset от казармы со стороны вражеского штаба, фронтом к нему.</summary>
        private static void SpawnSquad(Match match, Building barracks, BuildingSettings settings)
        {
            float2 direction = GetFrontDirection(match, barracks);
            float2 position = barracks.Position + direction * settings.spawnOffset;
            match.AddSquad(barracks.Owner, position, Angles.Of(direction), Formation.Line);
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
