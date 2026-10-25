using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Buildings
{
    /// <summary>
    /// Постройка казарм и найм: казарма с включённым наймом раз в hireInterval за hireCost получает рекрута
    /// в общий резерв стороны, пока у казарм ждут меньше maxWaitingRecruits человек.
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

        /// <summary>Рекрут приходит, когда истёк таймер, хватает золота и у казарм ещё есть место; иначе ждёт.</summary>
        private static void Hire(Match match, Building barracks, BuildingSettings settings, float deltaTime)
        {
            barracks.HireRemaining = math.max(0f, barracks.HireRemaining - deltaTime);
            if (barracks.HireRemaining > TimerEpsilon)
            {
                return;
            }

            PlayerState player = match.GetPlayer(barracks.Owner);
            if (player.Gold < settings.hireCost || player.Reserve >= settings.maxWaitingRecruits)
            {
                return;
            }

            player.Gold -= settings.hireCost;
            player.Reserve++;
            barracks.HireRemaining = settings.hireInterval;
        }

        /// <summary>Отряд появляется в spawnOffset от казармы со стороны вражеского штаба, фронтом к нему.</summary>
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
