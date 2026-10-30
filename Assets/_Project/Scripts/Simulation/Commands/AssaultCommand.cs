using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Melee;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Commands
{
    /// <summary>«В штыки» на вражеское здание. Заменяет текущий приказ и сбивает прицеливание.</summary>
    public sealed class AssaultCommand : ICommand
    {
        public AssaultCommand(int squadId, int targetBuildingId)
        {
            SquadId = squadId;
            TargetBuildingId = targetBuildingId;
        }

        public int SquadId { get; }

        public int TargetBuildingId { get; }

        public void Execute(Match match)
        {
            Squad squad = match.FindSquad(SquadId);
            Building target = match.FindBuilding(TargetBuildingId);
            if (!ChargeRules.CanAssault(squad, target))
            {
                return;
            }

            FireSystem.CancelAiming(squad);
            squad.Order = SquadOrder.Assault(target.Id);
        }
    }
}
