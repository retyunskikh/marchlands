using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Commands
{
    /// <summary>Идти к точке и встать фронтом в заданном направлении. Заменяет текущий приказ.</summary>
    public sealed class MoveCommand : ICommand
    {
        public MoveCommand(int squadId, float2 target, float finalFacing)
        {
            SquadId = squadId;
            Target = target;
            FinalFacing = finalFacing;
        }

        public int SquadId { get; }

        public float2 Target { get; }

        public float FinalFacing { get; }

        public void Execute(Match match)
        {
            Squad squad = match.FindSquad(SquadId);
            if (squad == null)
            {
                return;
            }

            squad.Order = SquadOrder.Move(Target, Angles.Normalize(FinalFacing));
        }
    }
}
