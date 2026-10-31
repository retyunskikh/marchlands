using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Commands
{
    /// <summary>«Стоп»: отменить приказ движения. Стрельбу и перестроение не трогает.</summary>
    public sealed class StopCommand : ICommand
    {
        public StopCommand(int squadId)
        {
            SquadId = squadId;
        }

        public int SquadId { get; }

        public void Execute(Match match)
        {
            Squad squad = match.FindSquad(SquadId);
            if (squad != null && squad.CanReceiveOrders)
            {
                squad.ClearRoute();
                squad.Order = SquadOrder.None;
            }
        }
    }
}
