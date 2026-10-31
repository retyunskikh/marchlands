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
    /// Разрушенный блокгауз отстраивают заново: когда у руин repairDelay секунд нет врагов (базу отбили),
    /// на их месте закладывается блокгауз, и рабочие строят его blockhouseRebuildTime, пока рядом нет врагов.
    /// </summary>
    internal static class RepairSystem
    {
        public static void Update(Match match, float deltaTime)
        {
            BuildingSettings settings = match.Settings.buildings;
            UpdateRuins(match, settings, deltaTime);
            foreach (Building building in match.Buildings)
            {
                building.SinceDamage += deltaTime;
                bool workers = HasWorkers(match, building.Owner) && !IsEnemyNear(match, building.Owner, building.Position, settings.repairEnemyRadius);
                bool repairing = building.IsBuilt
                    && !building.IsDestroyed
                    && building.Health < building.MaxHealth
                    && building.SinceDamage >= settings.repairDelay
                    && workers;
                building.IsRepairing = repairing;
                // Отстраиваемый блокгауз строят рабочие: при враге рядом стройка стоит (см. BuildingSystem).
                building.HasWorkers = repairing || ((!building.IsBuilt || building.IsUpgrading) && workers);
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

        /// <summary>Руины, у которых repairDelay секунд нет врагов, а штаб цел, — закладываются заново.</summary>
        private static void UpdateRuins(Match match, BuildingSettings settings, float deltaTime)
        {
            for (int i = match.BlockhouseRuins.Count - 1; i >= 0; i--)
            {
                BlockhouseRuin ruin = match.BlockhouseRuins[i];
                ruin.Quiet = IsEnemyNear(match, ruin.Owner, ruin.Position, settings.repairEnemyRadius) ? 0f : ruin.Quiet + deltaTime;
                if (ruin.Quiet >= settings.repairDelay && HasWorkers(match, ruin.Owner) && settings.blockhouseRebuildTime > 0f)
                {
                    match.RebuildBlockhouse(ruin);
                }
            }
        }

        private static bool IsEnemyNear(Match match, Side owner, float2 position, float radius)
        {
            float radiusSq = radius * radius;
            foreach (Squad squad in match.Squads)
            {
                if (squad.Side != owner && !squad.IsRouting && !squad.IsCamp && squad.Alive > 0
                    && math.distancesq(squad.Position, position) <= radiusSq)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
