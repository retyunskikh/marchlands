using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;

namespace Marchlands.Simulation.Commands
{
    /// <summary>Включить или выключить найм в казарме: пока включён и хватает золота, к ней приходят рекруты.</summary>
    public sealed class SetHiringCommand : ICommand
    {
        public SetHiringCommand(int buildingId, bool hiring)
        {
            BuildingId = buildingId;
            Hiring = hiring;
        }

        public int BuildingId { get; }

        public bool Hiring { get; }

        public void Execute(Match match)
        {
            Building barracks = match.FindBuilding(BuildingId);
            if (barracks == null || barracks.Type != BuildingType.Barracks || barracks.IsHiring == Hiring)
            {
                return;
            }

            barracks.IsHiring = Hiring;
            barracks.HireRemaining = Hiring ? match.Settings.buildings.hireInterval : 0f;
        }
    }
}
