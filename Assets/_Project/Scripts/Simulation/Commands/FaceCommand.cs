using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Commands
{
    /// <summary>
    /// Развернуться на месте фронтом в заданном направлении (кольцо разворота). Марш при этом не отменяется,
    /// а откладывается: отряд встаёт и разворачивается (например, чтобы дать залп), а когда рядом не останется
    /// не бегущих врагов, сам пойдёт дальше по маршруту.
    /// </summary>
    public sealed class FaceCommand : ICommand
    {
        public FaceCommand(int squadId, float facing)
        {
            SquadId = squadId;
            Facing = facing;
        }

        public int SquadId { get; }

        public float Facing { get; }

        public void Execute(Match match)
        {
            Squad squad = match.FindSquad(SquadId);
            if (squad == null || !squad.CanReceiveOrders)
            {
                return;
            }

            if (squad.Order.Type == SquadOrderType.Move && !squad.SuspendedOrder.HasValue)
            {
                squad.SuspendedOrder = squad.Order;
            }
            else if (!squad.SuspendedOrder.HasValue)
            {
                squad.ClearRoute();
            }

            // Развернули нарочно — отложенный марш ждёт, пока рядом не станет врагов (даже если его отложила рукопашная).
            squad.SuspendedByMelee = false;

            squad.HasArrived = false;
            squad.BestDistance = float.MaxValue;
            squad.StallTime = 0f;
            squad.Order = SquadOrder.Move(squad.Position, Angles.Normalize(Facing));
        }
    }
}
