using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;

namespace Marchlands.Simulation.Commands
{
    /// <summary>
    /// Заказать в артдепо одну пушку с расчётом: платят сразу (цена растёт с каждой живой пушкой стороны),
    /// через gunCrewHireInterval она встаёт у депо. Пока пушка готовится, следующую в этом депо не заказать.
    /// </summary>
    public sealed class OrderGunCommand : ICommand
    {
        public OrderGunCommand(int buildingId)
        {
            BuildingId = buildingId;
        }

        public int BuildingId { get; }

        public void Execute(Match match)
        {
            Building depot = match.FindBuilding(BuildingId);
            if (!BuildRules.CanOrderGun(match, depot))
            {
                return;
            }

            match.GetPlayer(depot.Owner).Gold -= BuildRules.GetGunCost(match, depot.Owner);
            depot.GunOrderRemaining = match.Settings.buildings.gunCrewHireInterval;
        }
    }
}
