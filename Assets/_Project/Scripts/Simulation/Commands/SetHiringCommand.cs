using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Commands
{
    /// <summary>
    /// Включить или выключить найм рода войск в казарме (гренадёры, фузилёры) или конюшне (конница).
    /// Пока включён и хватает золота, люди приходят в резерв этого рода войск.
    /// </summary>
    public sealed class SetHiringCommand : ICommand
    {
        public SetHiringCommand(int buildingId, UnitType type, bool hiring)
        {
            BuildingId = buildingId;
            Type = type;
            Hiring = hiring;
        }

        public int BuildingId { get; }

        public UnitType Type { get; }

        public bool Hiring { get; }

        public void Execute(Match match)
        {
            Building building = match.FindBuilding(BuildingId);
            if (building == null || !BuildRules.CanHireType(building, Type) || building.IsHiring(Type) == Hiring)
            {
                return;
            }

            bool wasHiring = building.IsHiringAny;
            building.SetHiring(Type, Hiring);
            if (!wasHiring && Hiring)
            {
                building.HireRemaining = BuildRules.GetHireInterval(match, building);
            }
        }
    }
}
