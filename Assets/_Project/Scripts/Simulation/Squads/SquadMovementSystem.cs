using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Unity.Mathematics;

namespace Marchlands.Simulation.Squads
{
    /// <summary>
    /// Движение отряда: перестроение; приказ движения (разворот лицом к точке → марш вперёд → разворот
    /// в конечный фронт); бег в штыки; бегство. Время внутри тика переносится между фазами, поэтому
    /// длительность приказа не зависит от того, на какой тик пришлась смена фазы.
    /// </summary>
    internal static class SquadMovementSystem
    {
        public static void Update(Squad squad, Match match, float deltaTime)
        {
            SquadSettings settings = match.Settings.squads;

            if (squad.IsInMelee)
            {
                return;
            }

            if (squad.IsRouting)
            {
                UpdateRout(squad, match, deltaTime);
                return;
            }

            if (squad.IsChangingFormation)
            {
                UpdateFormationChange(squad, settings, deltaTime);
                // Во время перестроения отряд стоит; приказ движения продолжится после него.
                return;
            }

            switch (squad.Order.Type)
            {
                case SquadOrderType.Move when squad.FireState != FireState.Aiming:
                    // На время прицеливания отряд стоит; приказ продолжится после залпа.
                    UpdateMove(squad, match, deltaTime);
                    break;
                case SquadOrderType.Charge:
                    UpdateCharge(squad, match, deltaTime);
                    break;
            }

            KeepInsideTerrain(squad, match);
        }

        /// <summary>Не даёт отряду выйти за непроходимую границу (горы и реки) с учётом его габаритов.</summary>
        public static void KeepInsideTerrain(Squad squad, Match match)
        {
            if (match.Terrain != null)
            {
                squad.Position = match.Terrain.Clamp(squad.Position, ProjectedHalfExtents(squad, squad.Facing, match.Settings.squads));
            }
        }

        /// <summary>Полуразмеры повёрнутого прямоугольника отряда по осям мира.</summary>
        public static float2 ProjectedHalfExtents(Squad squad, float facing, SquadSettings settings)
        {
            float2 half = FormationLayout.GetHalfExtents(squad.TargetFormation, squad.Alive, settings);
            float2 forward = Angles.Direction(facing);
            float2 right = Angles.Right(facing);
            return new float2(
                math.abs(half.x * right.x) + math.abs(half.y * forward.x),
                math.abs(half.x * right.y) + math.abs(half.y * forward.y));
        }

        public static float GetSpeed(Formation formation, SquadSettings settings)
        {
            return formation == Formation.Line ? settings.lineSpeed : settings.columnSpeed;
        }

        /// <summary>Прерывает перестроение: отряд остаётся в прежнем строю.</summary>
        public static void CancelFormationChange(Squad squad)
        {
            squad.FormationChangeRemaining = 0f;
            squad.FormationChangeProgress = 0f;
            squad.TargetFormation = squad.Formation;
        }

        private static void UpdateMove(Squad squad, Match match, float deltaTime)
        {
            SimulationSettings settings = match.Settings;
            float time = deltaTime;
            float2 target = squad.Order.Target;
            float2 toTarget = target - squad.Position;
            float distance = math.length(toTarget);

            if (distance > settings.squads.arrivalTolerance)
            {
                if (!RotateTowards(squad, Angles.Of(toTarget), settings.squads.rotationSpeed, ref time))
                {
                    return;
                }

                float speed = GetSpeed(squad.Formation, settings.squads);
                if (squad.FireState == FireState.Reloading)
                {
                    speed *= settings.combat.reloadMoveSpeedMultiplier;
                }

                speed *= match.TerrainSpeedMultiplier(squad.Position);
                if (speed <= 0f)
                {
                    return;
                }

                float step = speed * time;
                if (step < distance)
                {
                    squad.Position += toTarget / distance * step;
                    return;
                }

                squad.Position = target;
                time -= distance / speed;
            }

            if (RotateTowards(squad, squad.Order.FinalFacing, settings.squads.rotationSpeed, ref time))
            {
                squad.Order = SquadOrder.None;
            }
        }

        /// <summary>Разворот к центру цели и бег к ней; отряд останавливается при касании, не заходя внутрь.</summary>
        private static void UpdateCharge(Squad squad, Match match, float deltaTime)
        {
            Squad target = match.FindSquad(squad.Order.TargetSquadId);
            if (target == null)
            {
                return;
            }

            float time = deltaTime;
            float2 toTarget = target.Position - squad.Position;
            if (math.lengthsq(toTarget) <= 0f
                || !RotateTowards(squad, Angles.Of(toTarget), match.Settings.squads.rotationSpeed, ref time))
            {
                return;
            }

            float distance = FireGeometry.Distance(squad, target, match.Settings.squads);
            float speed = match.Settings.melee.chargeSpeed * match.TerrainSpeedMultiplier(squad.Position);
            float step = math.min(speed * time, distance);
            squad.Position += math.normalize(toTarget) * step;
        }

        /// <summary>Бегство: сразу лицом к точке отхода и бегом к ней; там отряд стоит и ждёт сбора.</summary>
        private static void UpdateRout(Squad squad, Match match, float deltaTime)
        {
            float2 direction;
            float distance;
            if (match.TryGetRetreatPoint(squad.Side, squad.Position, out float2 retreatPoint))
            {
                float2 toPoint = retreatPoint - squad.Position;
                distance = math.length(toPoint);
                if (distance <= match.Settings.squads.arrivalTolerance)
                {
                    return;
                }

                direction = toPoint / distance;
            }
            else
            {
                // Точки отхода нет — бежим прочь от ближайшего врага.
                Squad enemy = FindNearestEnemy(match, squad);
                if (enemy == null)
                {
                    return;
                }

                float2 away = squad.Position - enemy.Position;
                direction = math.lengthsq(away) > 0f ? math.normalize(away) : Angles.Direction(squad.Facing + 180f);
                distance = float.MaxValue;
            }

            squad.Facing = Angles.Of(direction);
            float speed = match.Settings.morale.routSpeed * match.TerrainSpeedMultiplier(squad.Position);
            squad.Position += direction * math.min(speed * deltaTime, distance);
        }

        private static Squad FindNearestEnemy(Match match, Squad squad)
        {
            Squad nearest = null;
            float best = float.MaxValue;
            foreach (Squad other in match.Squads)
            {
                if (other.Side == squad.Side)
                {
                    continue;
                }

                float distance = math.distancesq(other.Position, squad.Position);
                if (distance < best)
                {
                    best = distance;
                    nearest = other;
                }
            }

            return nearest;
        }

        private static void UpdateFormationChange(Squad squad, SquadSettings settings, float deltaTime)
        {
            squad.FormationChangeRemaining -= deltaTime;
            if (squad.FormationChangeRemaining > 0f)
            {
                squad.FormationChangeProgress = 1f - squad.FormationChangeRemaining / settings.formationChangeTime;
                return;
            }

            squad.FormationChangeRemaining = 0f;
            squad.FormationChangeProgress = 0f;
            squad.Formation = squad.TargetFormation;
        }

        /// <summary>
        /// Поворачивает фронт к <paramref name="targetFacing"/>, расходуя <paramref name="time"/>.
        /// Возвращает true, если направление достигнуто; тогда в <paramref name="time"/> остаётся неизрасходованное время.
        /// </summary>
        private static bool RotateTowards(Squad squad, float targetFacing, float rotationSpeed, ref float time)
        {
            float delta = Angles.Delta(squad.Facing, targetFacing);
            float needed = math.abs(delta) / rotationSpeed;
            if (needed <= time)
            {
                squad.Facing = Angles.Normalize(targetFacing);
                time -= needed;
                return true;
            }

            squad.Facing = Angles.Normalize(squad.Facing + math.sign(delta) * rotationSpeed * time);
            time = 0f;
            return false;
        }
    }
}
