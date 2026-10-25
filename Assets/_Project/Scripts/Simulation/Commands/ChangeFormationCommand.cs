using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Commands
{
    /// <summary>
    /// Начать перестроение. Игнорируется, если отряд уже в этом строю или ещё не закончил предыдущее перестроение.
    /// </summary>
    public sealed class ChangeFormationCommand : ICommand
    {
        public ChangeFormationCommand(int squadId, Formation formation)
        {
            SquadId = squadId;
            Formation = formation;
        }

        public int SquadId { get; }

        public Formation Formation { get; }

        public void Execute(Match match)
        {
            Squad squad = match.FindSquad(SquadId);
            if (squad == null || !squad.CanReceiveOrders || squad.IsChangingFormation || squad.Formation == Formation)
            {
                return;
            }

            // Перестроение сбивает прицеливание; отряд остаётся заряженным.
            FireSystem.CancelAiming(squad);

            float duration = match.Settings.squads.formationChangeTime;
            if (duration <= 0f)
            {
                squad.Formation = Formation;
                squad.TargetFormation = Formation;
                return;
            }

            squad.TargetFormation = Formation;
            squad.FormationChangeRemaining = duration;
            squad.FormationChangeProgress = 0f;
        }
    }
}
