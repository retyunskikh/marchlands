using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Unity.Mathematics;

namespace Marchlands.Simulation.Squads
{
    /// <summary>
    /// Перестроение и выполнение приказа движения: разворот лицом к точке → марш вперёд → разворот
    /// в конечный фронт. Время внутри тика переносится между фазами, поэтому длительность приказа
    /// не зависит от того, на какой тик пришлась смена фазы.
    /// </summary>
    internal static class SquadMovementSystem
    {
        public static void Update(Squad squad, SquadSettings settings, CombatSettings combat, float deltaTime)
        {
            if (squad.IsChangingFormation)
            {
                UpdateFormationChange(squad, settings, deltaTime);
                // Во время перестроения отряд стоит; приказ движения продолжится после него.
                return;
            }

            // На время прицеливания отряд останавливается; приказ продолжится после залпа.
            if (squad.Order.Type != SquadOrderType.Move || squad.FireState == FireState.Aiming)
            {
                return;
            }

            float time = deltaTime;
            float2 target = squad.Order.Target;
            float2 toTarget = target - squad.Position;
            float distance = math.length(toTarget);

            if (distance > settings.arrivalTolerance)
            {
                if (!RotateTowards(squad, Angles.Of(toTarget), settings.rotationSpeed, ref time))
                {
                    return;
                }

                float speed = GetSpeed(squad.Formation, settings);
                if (squad.FireState == FireState.Reloading)
                {
                    speed *= combat.reloadMoveSpeedMultiplier;
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

            if (RotateTowards(squad, squad.Order.FinalFacing, settings.rotationSpeed, ref time))
            {
                squad.Order = SquadOrder.None;
            }
        }

        public static float GetSpeed(Formation formation, SquadSettings settings)
        {
            return formation == Formation.Line ? settings.lineSpeed : settings.columnSpeed;
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
