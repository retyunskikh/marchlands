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
                target = match.Terrain.OutOfWater(target, squad.Position, match.Settings.terrain.bridgeHalfWidth);
            }

            squad.ClearRoute();
            // Немного назад — отряд пятится, не разворачиваясь, и встаёт прежним фронтом.
            if (IsBackstep(squad, target, match.Settings.squads))
            {
                facing = squad.Facing;
            }

            squad.HasArrived = false;
            squad.BestDistance = float.MaxValue;
            squad.StallTime = 0f;
            squad.Order = SquadOrder.Move(target, facing);
            if (AutoRoute)
            {
                RoutePlanner.TryPlan(match, squad, target);
            }
        }

        /// <summary>Точка позади отряда (за его тылом в секторе ±60°) и ближе backstepDistance — отойти шагом назад.</summary>
        public static bool IsBackstep(Squad squad, float2 target, Config.SquadSettings settings)
        {
            float2 offset = target - squad.Position;
            float distance = math.length(offset);
            return settings.backstepDistance > 0f && distance > settings.arrivalTolerance && distance <= settings.backstepDistance
                && math.abs(Angles.Delta(squad.Facing, Angles.Of(offset))) >= BackstepAngle;
        }

        // Насколько точка должна быть позади (угол от фронта), чтобы отряд пятился, градусы.
        private const float BackstepAngle = 120f;
    }
}
