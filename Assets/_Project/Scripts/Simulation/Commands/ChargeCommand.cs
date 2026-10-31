using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Melee;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Commands
{
    /// <summary>«В штыки» на выбранный вражеский отряд. Заменяет текущий приказ и сбивает прицеливание.</summary>
    public sealed class ChargeCommand : ICommand
    {
        public ChargeCommand(int squadId, int targetSquadId)
        {
            SquadId = squadId;
            TargetSquadId = targetSquadId;
        }

        public int SquadId { get; }

        public int TargetSquadId { get; }

        public void Execute(Match match)
        {
            Squad squad = match.FindSquad(SquadId);
            Squad target = match.FindSquad(TargetSquadId);
            if (!ChargeRules.CanCharge(squad, target))
            {
                return;
            }

            FireSystem.CancelAiming(squad);
            squad.ClearRoute();
            squad.Order = SquadOrder.Charge(target.Id);
            squad.ChargeChecked = false;
            squad.ChargeTime = 0f;
        }
    }
}
