using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Commands
{
    /// <summary>
    /// Идти к точке и встать фронтом в заданном направлении. Заменяет текущий приказ. С <see cref="AutoRoute"/>
    /// дальний приказ прокладывается по местности: отряд сам идёт к дороге, в колонне, если так быстрее.
    /// </summary>
    public sealed class MoveCommand : ICommand
    {
        public MoveCommand(int squadId, float2 target, float finalFacing, bool autoRoute = false)
        {
            SquadId = squadId;
            Target = target;
            FinalFacing = finalFacing;
            AutoRoute = autoRoute;
        }

        /// <summary>Прокладывать ли дальний путь по местности (приказы игрока); ИИ ведёт отряды по своим точкам пути.</summary>
        public bool AutoRoute { get; }

        public int SquadId { get; }

        public float2 Target { get; }

        public float FinalFacing { get; }

        public void Execute(Match match)
        {
            Squad squad = match.FindSquad(SquadId);
            if (squad == null || !squad.CanReceiveOrders)
            {
                return;
            }

            // Точка за горами или рекой прижимается к границе так, чтобы отряд в ней поместился.
            float facing = Angles.Normalize(FinalFacing);
            float2 target = Target;
            if (match.Terrain != null)
            {
                target = match.Terrain.Clamp(target, SquadMovementSystem.ProjectedHalfExtents(squad, facing, match.Settings.squads));
                // Точка в реке — на ближний берег (с той стороны, где отряд).
                target = match.Terrain.OutOfWater(target, squad.Position);
            }

            squad.ClearRoute();
            squad.Order = SquadOrder.Move(target, facing);
            if (AutoRoute)
            {
                RoutePlanner.TryPlan(match, squad, target);
            }
        }
    }
}
