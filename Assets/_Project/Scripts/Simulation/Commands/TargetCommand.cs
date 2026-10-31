using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Commands
{
    /// <summary>
    /// Указать пушке цель: пока цель жива и её можно достать, пушка бьёт по ней, а не по ближайшему врагу.
    /// 0 — снова по ближайшему.
    /// </summary>
    public sealed class TargetCommand : ICommand
    {
        public TargetCommand(int squadId, int targetSquadId)
        {
            SquadId = squadId;
            TargetSquadId = targetSquadId;
        }

        public int SquadId { get; }

        public int TargetSquadId { get; }

        public void Execute(Match match)
        {
            Squad squad = match.FindSquad(SquadId);
            if (squad == null || !squad.IsArtillery || !squad.CanReceiveOrders)
            {
                return;
            }

            Squad target = match.FindSquad(TargetSquadId);
            squad.PreferredTargetId = target != null && target.Side != squad.Side ? target.Id : 0;
            ArtillerySystem.CancelAiming(squad);
        }
    }
}
