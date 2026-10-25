using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;

namespace Marchlands.Simulation.Commands
{
    /// <summary>Поставить в очередь казармы отряд линейной пехоты. Золото списывается сразу.</summary>
    public sealed class RecruitCommand : ICommand
    {
        public RecruitCommand(int buildingId)
        {
            BuildingId = buildingId;
        }

        public int BuildingId { get; }

        public void Execute(Match match)
        {
            Building barracks = match.FindBuilding(BuildingId);
            if (!BuildRules.CanRecruit(match, barracks))
            {
                return;
            }

            match.GetPlayer(barracks.Owner).Gold -= match.Settings.buildings.squadCost;
            barracks.RecruitQueue++;
            if (barracks.RecruitQueue == 1)
            {
                barracks.RecruitRemaining = match.Settings.buildings.recruitTime;
            }
        }
    }
}
