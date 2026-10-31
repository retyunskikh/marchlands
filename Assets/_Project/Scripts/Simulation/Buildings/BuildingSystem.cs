using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Buildings
{
    /// <summary>
    /// Постройка зданий и найм. Казарма получает рекрута в резерв включённого рода войск (фузилёры, гренадёры —
    /// по очереди, если включены оба), конюшня — всадника; цена и время найма у каждого рода войск свои.
    /// Род войск, у которого ждут людей на maxWaitingSquads отрядов, пропускается; без золота найм ждёт.
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
                    // Блокгауз отстраивают рабочие из штаба: враг рядом — стройка стоит.
                    if (building.Type != BuildingType.Blockhouse || building.HasWorkers)
                    {
                        building.BuildRemaining -= deltaTime;
                    }

                    if (building.BuildRemaining <= TimerEpsilon)
                    {
                        building.BuildRemaining = 0f;
                    }

                    continue;
                }

                if (building.IsBuildingHouse)
                {
                    building.HouseBuildRemaining -= deltaTime;
                    if (building.HouseBuildRemaining <= TimerEpsilon)
                    {
                        building.HouseBuildRemaining = 0f;
                        building.Houses++;
                    }
                }

                // Усиление казармы: строят рабочие, найм в это время стоит.
                if (building.IsUpgrading)
                {
                    building.UpgradeRemaining -= deltaTime;
                    if (building.UpgradeRemaining <= TimerEpsilon)
                    {
                        building.UpgradeRemaining = 0f;
                        building.Level++;
                    }
                }
                else if (building.IsHiringAny)
                {
                    Hire(match, building, settings, deltaTime);
                }

                // Заказанная пушка готова — встаёт у депо со всем расчётом.
                if (building.IsOrderingGun)
                {
                    building.GunOrderRemaining -= deltaTime;
                    if (building.GunOrderRemaining <= TimerEpsilon)
                    {
                        building.GunOrderRemaining = 0f;
                        match.ReportFormed(SpawnSquad(match, building, UnitType.Artillery));
                        if (building.GunOrdersQueued > 0)
                        {
                            building.GunOrdersQueued--;
                            building.GunOrderRemaining = settings.gunCrewHireInterval;
                        }
                    }
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

            UnitType? type = BuildRules.NextHireType(match, building);
            if (!type.HasValue)
            {
                return;
            }

            PlayerState player = match.GetPlayer(building.Owner);
            player.Gold -= BuildRules.GetHireCost(settings, type.Value);
            player.AddReserve(type.Value, 1);

            building.NextHireType = (System.Array.IndexOf(BuildRules.HireOrder, type.Value) + 1) % BuildRules.HireOrder.Length;
            building.HireRemaining = BuildRules.GetHireInterval(match, building);
        }

        /// <summary>Отряд появляется в spawnOffset от здания со стороны вражеского штаба, фронтом к нему.</summary>
        internal static Squad SpawnSquad(Match match, Building barracks, UnitType type)
        {
            float2 direction = GetFrontDirection(match, barracks);
            float2 position = barracks.Position + direction * match.Settings.buildings.spawnOffset;
            return match.AddSquad(barracks.Owner, position, Angles.Of(direction), Formation.Line, type);
        }

        internal static float2 GetFrontDirection(Match match, Building barracks)
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
