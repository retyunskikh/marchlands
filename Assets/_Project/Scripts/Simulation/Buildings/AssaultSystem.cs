using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Buildings
{
    /// <summary>
    /// Штурм зданий в штыки: отряд с приказом «штурм», дошедший до assaultRange от центра здания и не занятый
    /// рукопашной, каждую секунду снимает assaultDamage здоровья за каждого солдата (× meleeMultiplier рода войск).
    /// На нуле здание разрушено, штурмовавшие его отряды останавливаются.
    /// </summary>
    internal static class AssaultSystem
    {
        // Отряд останавливается ровно на assaultRange, а float может дать чуть больше.
        private const float RangeEpsilon = 1e-3f;

        public static void Update(Match match, float deltaTime)
        {
            BuildingSettings settings = match.Settings.buildings;
            foreach (Squad squad in match.Squads)
            {
                if (squad.Order.Type != SquadOrderType.Assault || squad.IsRouting || squad.IsInMelee || squad.Alive <= 0)
                {
                    continue;
                }

                Building target = match.FindBuilding(squad.Order.TargetBuildingId);
                if (target == null || target.IsDestroyed)
                {
                    continue;
                }

                if (math.distance(squad.Position, target.Position) > settings.assaultRange + RangeEpsilon)
                {
                    continue;
                }

                target.Health -= squad.Alive * settings.assaultDamage * UnitStats.Of(squad, match.Settings).meleeMultiplier * deltaTime;
                if (target.Health <= 0f)
                {
                    match.DestroyBuilding(target);
                }
            }

            foreach (Squad squad in match.Squads)
            {
                if (squad.Order.Type == SquadOrderType.Assault)
                {
                    Building target = match.FindBuilding(squad.Order.TargetBuildingId);
                    if (target == null || target.IsDestroyed)
                    {
                        squad.Order = SquadOrder.None;
                    }
                }
            }
        }
    }
}
