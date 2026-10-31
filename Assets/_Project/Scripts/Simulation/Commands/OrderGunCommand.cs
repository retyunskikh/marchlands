using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;

namespace Marchlands.Simulation.Commands
{
    /// <summary>
    /// Заказать в артдепо одну пушку с расчётом: платят сразу (цена растёт с каждой живой пушкой стороны),
    /// через gunCrewHireInterval она встаёт у депо. Пока одна готовится, ещё до gunQueueSize ждут в очереди.
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
            if (depot.IsOrderingGun)
            {
                // Пока готовят одну, следующая ждёт в очереди (уже оплачена).
                depot.GunOrdersQueued++;
                return;
            }

            depot.GunOrderRemaining = match.Settings.buildings.gunCrewHireInterval;
        }
    }
}
