using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;

namespace Marchlands.Simulation.Commands
{
    /// <summary>
    /// Усилить казарму: платят сразу, рабочие строят barracksUpgradeTime (казарма в это время не нанимает); потом
    /// найм в barracksUpgradeHireSpeed раз быстрее, часовых больше, казарма крупнее.
    /// </summary>
    public sealed class UpgradeBarracksCommand : ICommand
    {
        public UpgradeBarracksCommand(int buildingId)
        {
            BuildingId = buildingId;
        }

        public int BuildingId { get; }

        public void Execute(Match match)
        {
            Building barracks = match.FindBuilding(BuildingId);
            if (!BuildRules.CanUpgrade(match, barracks))
            {
                return;
            }

            match.GetPlayer(barracks.Owner).Gold -= match.Settings.buildings.barracksUpgradeCost;
            barracks.UpgradeRemaining = match.Settings.buildings.barracksUpgradeTime;
        }
    }
}
