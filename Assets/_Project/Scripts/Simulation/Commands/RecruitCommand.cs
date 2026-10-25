using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;

namespace Marchlands.Simulation.Commands
{
    /// <summary>
    /// Поставить в очередь казармы отряд линейной пехоты. Если в резерве набрался полный отряд, он бесплатный
    /// и берёт людей из резерва; иначе золото списывается сразу.
    /// </summary>
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

            PlayerState player = match.GetPlayer(barracks.Owner);
            if (BuildRules.RecruitsFromReserve(match, barracks.Owner))
            {
                player.Reserve -= match.Settings.squads.squadSize;
            }
            else
            {
                player.Gold -= match.Settings.buildings.squadCost;
            }

            barracks.RecruitQueue++;
            if (barracks.RecruitQueue == 1)
            {
                barracks.RecruitRemaining = match.Settings.buildings.recruitTime;
            }
        }
    }
}
