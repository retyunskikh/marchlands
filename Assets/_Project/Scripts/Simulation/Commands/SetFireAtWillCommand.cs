using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Commands
{
    /// <summary>Включить или выключить «огонь по готовности».</summary>
    public sealed class SetFireAtWillCommand : ICommand
    {
        public SetFireAtWillCommand(int squadId, bool enabled)
        {
            SquadId = squadId;
            Enabled = enabled;
        }

        public int SquadId { get; }

        public bool Enabled { get; }

        public void Execute(Match match)
        {
            Squad squad = match.FindSquad(SquadId);
            if (squad != null)
            {
                squad.FireAtWill = Enabled;
            }
        }
    }
}
