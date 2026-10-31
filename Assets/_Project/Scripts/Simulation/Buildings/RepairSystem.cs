using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Buildings
{
    /// <summary>
    /// Починка зданий: повреждённое здание, которое repairDelay секунд не получало урона и рядом с которым
    /// (repairEnemyRadius) нет не бегущих врагов, чинят рабочие из штаба — repairRate здоровья в секунду, бесплатно.
    /// Без своего штаба чинить некому. Рабочие только для вида: их нельзя убить.
    /// </summary>
    internal static class RepairSystem
    {
        public static void Update(Match match, float deltaTime)
        {
            BuildingSettings settings = match.Settings.buildings;
            foreach (Building building in match.Buildings)
            {
                building.SinceDamage += deltaTime;
                bool repairing = !building.IsDestroyed
                    && building.Health < building.MaxHealth
                    && building.SinceDamage >= settings.repairDelay
                    && HasWorkers(match, building.Owner)
                    && !IsEnemyNear(match, building, settings.repairEnemyRadius);
                building.IsRepairing = repairing;
                if (repairing)
                {
                    building.Health = math.min(building.MaxHealth, building.Health + settings.repairRate * deltaTime);
                }
            }
        }

        private static bool HasWorkers(Match match, Side side)
        {
            Building headquarters = match.GetHeadquarters(side);
            return headquarters != null && !headquarters.IsDestroyed;
        }

        private static bool IsEnemyNear(Match match, Building building, float radius)
        {
            float radiusSq = radius * radius;
            foreach (Squad squad in match.Squads)
            {
                if (squad.Side != building.Owner && !squad.IsRouting && !squad.IsCamp && squad.Alive > 0
                    && math.distancesq(squad.Position, building.Position) <= radiusSq)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
