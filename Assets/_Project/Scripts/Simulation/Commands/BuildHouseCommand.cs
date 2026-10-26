using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;

namespace Marchlands.Simulation.Commands
{
    /// <summary>
    /// Пристроить к штабу дом: ещё housePopulation мест. Место не выбирают — дом встаёт пристройкой.
    /// Золото списывается сразу; строится по одному дому. Команда игнорируется, если дом сейчас нельзя заложить.
    /// </summary>
    public sealed class BuildHouseCommand : ICommand
    {
        public BuildHouseCommand(Side side)
        {
            Side = side;
        }

        public Side Side { get; }

        public void Execute(Match match)
        {
            if (!BuildRules.CanBuildHouse(match, Side))
            {
                return;
            }

            Building headquarters = match.GetHeadquarters(Side);
            match.GetPlayer(Side).Gold -= BuildRules.GetHouseCost(match, Side);
            float buildTime = match.Settings.buildings.houseBuildTime;
            if (buildTime <= 0f)
            {
                headquarters.Houses++;
                return;
            }

            headquarters.HouseBuildRemaining = buildTime;
        }
    }
}
