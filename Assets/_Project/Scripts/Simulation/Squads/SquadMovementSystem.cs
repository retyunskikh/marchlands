using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Unity.Mathematics;

namespace Marchlands.Simulation.Squads
{
    /// <summary>
    /// Движение отряда: перестроение; приказ движения (разворот лицом к точке → марш вперёд → разворот
    /// в конечный фронт); бег в штыки; бегство. Время внутри тика переносится между фазами, поэтому
    /// длительность приказа не зависит от того, на какой тик пришлась смена фазы. Свои отряды проходят
    /// друг сквозь друга, а во врага не войти и не развернуться сквозь него: такой шаг отменяется,
    /// и отряд остаётся вплотную — рукопашную начинает <see cref="Melee.MeleeSystem"/>.
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
                float2 routFrom = squad.Position;
                UpdateRout(squad, match, deltaTime);
                StayOutOfWater(squad, match, routFrom);
                return;
            }

            bool reforming = squad.IsChangingFormation;
            // Перестраиваясь (в колонну или в линию), отряд с приказом движения сразу идёт к цели, но медленно.
            bool marchingWhileReforming = reforming && squad.Order.Type == SquadOrderType.Move;
            if (reforming)
            {
                UpdateFormationChange(squad, settings, deltaTime);
            }

            if (reforming && !marchingWhileReforming)
            {
                return;
            }

            float2 position = squad.Position;
            float facing = squad.Facing;
            float overlapBefore = EnemyOverlap(squad, match);
            switch (squad.Order.Type)
            {
                case SquadOrderType.Move when squad.FireState != FireState.Aiming:
                    // На время прицеливания отряд стоит; приказ продолжится после залпа.
                    UpdateMove(squad, match, deltaTime, marchingWhileReforming ? settings.reformMoveSpeedMultiplier : 1f);
                    break;
                case SquadOrderType.Charge:
                    UpdateCharge(squad, match, deltaTime);
                    break;
                case SquadOrderType.Assault:
                    UpdateAssault(squad, match, deltaTime);
                    break;
            }

            KeepInsideTerrain(squad, match);

            // Шаг или поворот, который вдавливает строй во врага, отменяется (если уже перекрывались — нельзя глубже).
            if (EnemyOverlap(squad, match) > overlapBefore + OverlapEpsilon)
            {
                squad.Position = position;
                squad.Facing = facing;
            }

            StayOutOfWater(squad, match, position);
        }

        /// <summary>В реку вне переправ не войти: шаг в воду отменяется, отряд остаётся на берегу.</summary>
        private static void StayOutOfWater(Squad squad, Match match, float2 before)
        {
            if (match.Terrain != null && match.Terrain.IsWater(squad.Position) && !match.Terrain.IsWater(before))
            {
                squad.Position = before;
            }
        }

        /// <summary>Куда идти к <paramref name="goal"/>: напрямую или через переправу, если по пути река.</summary>
        private static float2 Steer(Squad squad, Match match, float2 goal)
        {
            return match.Terrain != null ? match.Terrain.Steer(squad.Position, goal) : goal;
        }

        /// <summary>Атака вверх по склону холма медленнее.</summary>
        private static float UphillMultiplier(Squad squad, Match match, float2 direction)
        {
            return match.Terrain != null && match.Terrain.IsUphill(squad.Position, direction) && match.Settings.terrain.uphillChargeMultiplier > 0f
                ? match.Settings.terrain.uphillChargeMultiplier
                : 1f;
        }

        private const float OverlapEpsilon = 1e-3f;

        /// <summary>Наибольшее перекрытие строя с не бегущими врагами, u.</summary>
        private static float EnemyOverlap(Squad squad, Match match)
        {
            SquadFootprint own = SquadFootprint.Of(squad, match.Settings.squads);
            float worst = 0f;
            foreach (Squad other in match.Squads)
            {
                if (other.Side == squad.Side || other.IsRouting || other.IsWithdrawn || other.Alive <= 0)
                {
                    continue;
                }

                worst = math.max(worst, SquadFootprint.PenetrationDepth(own, SquadFootprint.Of(other, match.Settings.squads)));
            }

            return worst;
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

        private static void UpdateMove(Squad squad, Match match, float deltaTime, float speedMultiplier)
        {
            SimulationSettings settings = match.Settings;
            float time = deltaTime;
            // Если по пути река — сначала к переправе (точка пути), дальше снова к цели приказа.
            float2 target = Steer(squad, match, squad.Order.Target);
            bool detour = math.any(target != squad.Order.Target);
            float2 toTarget = target - squad.Position;
            float distance = math.length(toTarget);

            if (distance > settings.squads.arrivalTolerance)
            {
                if (!RotateTowards(squad, Angles.Of(toTarget), settings.squads.rotationSpeed, ref time))
                {
                    return;
                }

                float speed = UnitStats.Speed(squad, squad.Formation, settings) * speedMultiplier;
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

            // Дошли до точки переправы — приказ продолжается со следующего тика.
            if (detour)
            {
                return;
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
            float2 toTarget = Steer(squad, match, target.Position) - squad.Position;
            if (math.lengthsq(toTarget) <= 0f
                || !RotateTowards(squad, Angles.Of(toTarget), match.Settings.squads.rotationSpeed, ref time))
            {
                return;
            }

            float distance = math.min(FireGeometry.Distance(squad, target, match.Settings.squads), math.length(toTarget));
            float2 direction = math.normalize(toTarget);
            float speed = UnitStats.ChargeSpeed(squad, match.Settings) * match.TerrainSpeedMultiplier(squad.Position)
                * UphillMultiplier(squad, match, direction);
            float step = math.min(speed * time, distance);
            squad.Position += direction * step;
        }

        /// <summary>
        /// Бег в штыки на здание: разворот к нему и бег, пока центр отряда не окажется в assaultRange от центра здания.
        /// Удары по зданию наносит <see cref="Buildings.AssaultSystem"/>.
        /// </summary>
        private static void UpdateAssault(Squad squad, Match match, float deltaTime)
        {
            Buildings.Building target = match.FindBuilding(squad.Order.TargetBuildingId);
            if (target == null)
            {
                return;
            }

            float distance = math.length(target.Position - squad.Position) - match.Settings.buildings.assaultRange;
            float2 toTarget = Steer(squad, match, target.Position) - squad.Position;
            float time = deltaTime;
            if (distance <= 0f || math.lengthsq(toTarget) <= 0f
                || !RotateTowards(squad, Angles.Of(toTarget), match.Settings.squads.rotationSpeed, ref time))
            {
                return;
            }

            float2 direction = math.normalize(toTarget);
            float speed = UnitStats.ChargeSpeed(squad, match.Settings) * match.TerrainSpeedMultiplier(squad.Position)
                * UphillMultiplier(squad, match, direction);
            squad.Position += direction * math.min(speed * time, math.min(distance, math.length(toTarget)));
        }

        /// <summary>Бегство: разворот к точке отхода со скоростью routRotationSpeed и бегом к ней.</summary>
        private static void UpdateRout(Squad squad, Match match, float deltaTime)
        {
            float2 direction;
            float distance;
            if (match.TryGetRetreatPoint(squad, out float2 retreatPoint))
            {
                // Бегут к своим через переправу, если по прямой река.
                float2 toPoint = Steer(squad, match, retreatPoint) - squad.Position;
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

            float time = deltaTime;
            if (!RotateTowards(squad, Angles.Of(direction), match.Settings.morale.routRotationSpeed, ref time))
            {
                return;
            }

            float speed = match.Settings.morale.routSpeed * match.TerrainSpeedMultiplier(squad.Position);
            squad.Position += direction * math.min(speed * time, distance);
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
