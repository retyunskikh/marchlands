using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Commands
{
    /// <summary>
    /// Цель найма: здание приостанавливает найм, когда людей хватает на отряд этого рода войск
    /// (казарма — егеря или гренадёры, конюшня — конница); null — нанимать без остановки.
    /// </summary>
    public sealed class SetHireTargetCommand : ICommand
    {
        public SetHireTargetCommand(int buildingId, UnitType? target)
        {
            BuildingId = buildingId;
            Target = target;
        }

        public int BuildingId { get; }

        public UnitType? Target { get; }

        public void Execute(Match match)
        {
            Building building = match.FindBuilding(BuildingId);
            if (building == null || building.Type == BuildingType.Headquarters || !BuildRules.IsValidHireTarget(building, Target))
            {
                return;
            }

            building.HireUntil = Target;
        }
    }
}
