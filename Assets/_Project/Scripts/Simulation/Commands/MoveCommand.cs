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
                // Враг рядом — пехота в колонне разворачивается в линию, куда бы ни шла.
                if (squad.Type != UnitType.Cavalry && !squad.IsArtillery && squad.TargetFormation == Formation.Column
                    && RoutePlanner.IsEnemyNear(match, squad, match.Settings.squads.autoLineEnemyRadius))
                {
                    ChangeFormationCommand.Start(match, squad, Formation.Line);
                }

                // Недалеко и враг рядом (у отряда или у цели) — это манёвр на позицию: идут прямо, своим строем,
                // без перестроения и без крюка к дорогам. Дальний приказ — по дорогам (колонна сама развернётся
                // в линию, когда подойдёт враг).
                if (!IsManoeuvre(match, squad, target))
                {
                    RoutePlanner.TryPlan(match, squad, target);
                }
            }
        }

        /// <summary>Манёвр у врага: цель ближе autoColumnMinDistance, а враг ближе autoLineEnemyRadius к отряду или к цели.</summary>
        public static bool IsManoeuvre(Match match, Squad squad, float2 target)
        {
            Config.SquadSettings settings = match.Settings.squads;
            if (math.distance(squad.Position, target) >= settings.autoColumnMinDistance)
            {
                return false;
            }

            float radius = settings.autoLineEnemyRadius;
            if (RoutePlanner.IsEnemyNear(match, squad, radius))
            {
                return true;
            }

            foreach (Squad other in match.Squads)
            {
                if (other.Side != squad.Side && !other.IsRouting && !other.IsWithdrawn && !other.IsCamp && other.Alive > 0
                    && math.distancesq(other.Position, target) <= radius * radius)
                {
                    return true;
                }
            }

            return false;
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
