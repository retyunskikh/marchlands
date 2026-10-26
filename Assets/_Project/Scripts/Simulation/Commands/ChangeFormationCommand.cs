using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Commands
{
    /// <summary>
    /// Начать перестроение. Во время перестроения приказ вернуться в прежний строй разворачивает его с той же точки:
    /// возврат длится столько, сколько уже прошло. Игнорируется, если отряд уже в этом строю или идёт к нему.
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
            if (squad == null || !squad.CanReceiveOrders || squad.TargetFormation == Formation
                || (!squad.IsChangingFormation && squad.Formation == Formation))
            {
                return;
            }

            if (squad.IsChangingFormation)
            {
                float elapsed = match.Settings.squads.formationChangeTime - squad.FormationChangeRemaining;
                squad.Formation = squad.TargetFormation;
                squad.TargetFormation = Formation;
                squad.FormationChangeRemaining = elapsed;
                squad.FormationChangeProgress = 1f - squad.FormationChangeProgress;
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
