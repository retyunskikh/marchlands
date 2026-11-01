using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;

namespace Marchlands.Simulation.Commands
{
    /// <summary>
    /// Усилить (укрепить) здание: платят сразу, рабочие строят (под строительными лесами). Казарма в это время
    /// не нанимает, потом нанимает в barracksUpgradeHireSpeed раз быстрее и с большим числом часовых. Штаб и блокгауз
    /// из деревянных становятся каменными: крепче, у штаба больше часовых, у блокгауза — больше защитников и дальше огонь.
    /// </summary>
    public sealed class UpgradeBuildingCommand : ICommand
    {
        public UpgradeBuildingCommand(int buildingId)
        {
            BuildingId = buildingId;
        }

        public int BuildingId { get; }

        public void Execute(Match match)
        {
            Building building = match.FindBuilding(BuildingId);
            if (!BuildRules.CanUpgrade(match, building))
            {
                return;
            }

            match.GetPlayer(building.Owner).Gold -= BuildRules.GetUpgradeCost(match, building.Type);
            building.UpgradeRemaining = BuildRules.GetUpgradeTime(match, building.Type);
        }
    }
}
