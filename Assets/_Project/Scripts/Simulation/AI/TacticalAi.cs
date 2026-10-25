using System.Collections.Generic;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Melee;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.AI
{
    /// <summary>
    /// Тактический ИИ одной стороны (этап 4 docs/design.md). Для каждого отряда: наступать на ближайшего
    /// не бегущего врага, держа огонь до случайной дистанции между pointBlankRange и effectiveRange →
    /// залп → штыки, если враг перезаряжается рядом, а своя мораль высока → отход при низкой морали.
    /// Приказы отдаёт только командами, с задержкой реакции.
    /// </summary>
    public sealed class TacticalAi : IMatchController
    {
        private readonly Dictionary<int, SquadBrain> brains = new Dictionary<int, SquadBrain>();
        private int nextThinkTick;

        public TacticalAi(Side side)
        {
            Side = side;
        }

        public Side Side { get; }

        private enum Stance
        {
            Advance,
            Retreat
        }

        private sealed class SquadBrain
        {
            public Stance Stance;
            public int TargetId;
            public float FireDistance;

            /// <summary>До этого тика отряд ждёт исполнения прошлого приказа и не получает новых.</summary>
            public int BusyUntilTick = -1;
        }

        public void Update(Match match)
        {
            if (match.CurrentTick < nextThinkTick)
            {
                return;
            }

            AiSettings ai = match.Settings.ai;
            nextThinkTick = match.CurrentTick + math.max(1, SecondsToTicks(ai.thinkInterval));

            foreach (Squad squad in match.Squads)
            {
                if (squad.Side != Side)
                {
                    continue;
                }

                if (!brains.TryGetValue(squad.Id, out SquadBrain brain))
                {
                    brain = new SquadBrain();
                    brains.Add(squad.Id, brain);
                }

                if (match.CurrentTick > brain.BusyUntilTick)
                {
                    Think(match, squad, brain);
                }
            }
        }

        private void Think(Match match, Squad squad, SquadBrain brain)
        {
            AiSettings ai = match.Settings.ai;

            if (squad.IsRouting)
            {
                // Бегущий не слушается; после сбора выберет цель заново.
                brain.Stance = Stance.Advance;
                brain.TargetId = 0;
                return;
            }

            if (!squad.CanReceiveOrders || squad.Order.Type == SquadOrderType.Charge)
            {
                return;
            }

            if (brain.Stance == Stance.Retreat)
            {
                if (squad.Morale >= ai.returnMorale)
                {
                    brain.Stance = Stance.Advance;
                    brain.TargetId = 0;
                }
                else
                {
                    KeepRetreating(match, squad, brain);
                    return;
                }
            }

            if (squad.Morale < ai.retreatMorale)
            {
                brain.Stance = Stance.Retreat;
                KeepRetreating(match, squad, brain);
                return;
            }

            Squad target = SelectTarget(match, squad, brain);
            if (target == null)
            {
                if (squad.Order.Type == SquadOrderType.Move)
                {
                    Issue(match, brain, new StopCommand(squad.Id));
                }

                return;
            }

            float distance = FireGeometry.Distance(squad, target, match.Settings.squads);

            if (target.FireState == FireState.Reloading
                && distance < ai.chargeDistance
                && squad.Morale > ai.chargeMinMorale
                && ChargeRules.CanCharge(squad, target))
            {
                Issue(match, brain, new ChargeCommand(squad.Id, target.Id));
                return;
            }

            // Допуск: подход заканчивается ровно на дистанции огня, а float может дать чуть больше.
            if (distance <= brain.FireDistance + match.Settings.squads.arrivalTolerance)
            {
                HoldAndFire(match, squad, target, brain);
                return;
            }

            Approach(match, squad, target, brain);
        }

        /// <summary>Держит текущую цель, пока она жива и не бежит; иначе берёт ближайшего не бегущего врага.</summary>
        private static Squad SelectTarget(Match match, Squad squad, SquadBrain brain)
        {
            Squad current = match.FindSquad(brain.TargetId);
            if (current != null && !current.IsRouting)
            {
                return current;
            }

            Squad best = null;
            float bestDistance = float.MaxValue;
            foreach (Squad other in match.Squads)
            {
                if (other.Side == squad.Side || other.IsRouting)
                {
                    continue;
                }

                float distance = math.distancesq(other.Position, squad.Position);
                if (distance < bestDistance)
                {
                    best = other;
                    bestDistance = distance;
                }
            }

            brain.TargetId = best?.Id ?? 0;
            if (best != null)
            {
                // Верхняя граница чуть меньше effectiveRange: с допуском подхода залп всё равно в эффективной дальности.
                CombatSettings combat = match.Settings.combat;
                float maxDistance = combat.effectiveRange - match.Settings.squads.arrivalTolerance;
                brain.FireDistance = match.Random.Range(combat.pointBlankRange, maxDistance);
            }

            return best;
        }

        private void HoldAndFire(Match match, Squad squad, Squad target, SquadBrain brain)
        {
            if (!FireGeometry.IsInSector(squad, target.Position, match.Settings))
            {
                // Цель вне сектора огня — развернуться на месте.
                float facing = Angles.Of(target.Position - squad.Position);
                Issue(match, brain, new MoveCommand(squad.Id, squad.Position, facing));
                return;
            }

            if (squad.Order.Type == SquadOrderType.Move)
            {
                Issue(match, brain, new StopCommand(squad.Id));
                return;
            }

            if (VolleyRules.CanVolley(match, squad))
            {
                Issue(match, brain, new VolleyCommand(squad.Id, match.Settings.combat.effectiveRange));
            }
        }

        /// <summary>
        /// Точка подхода — перед целью на выбранной дистанции огня, фронтом к ней. Приказ обновляется,
        /// только если точка сместилась заметно, чтобы отряд не дёргался.
        /// </summary>
        private void Approach(Match match, Squad squad, Squad target, SquadBrain brain)
        {
            float2 toTarget = target.Position - squad.Position;
            if (math.lengthsq(toTarget) <= 0f)
            {
                return;
            }

            float2 direction = math.normalize(toTarget);
            SquadSettings settings = match.Settings.squads;
            float ownHalfDepth = FormationLayout.GetHalfExtents(squad.Formation, squad.Alive, settings).y;
            float targetHalfDepth = FormationLayout.GetHalfExtents(target.Formation, target.Alive, settings).y;
            float2 point = target.Position - direction * (brain.FireDistance + ownHalfDepth + targetHalfDepth);

            bool alreadyGoing = squad.Order.Type == SquadOrderType.Move
                && math.distance(squad.Order.Target, point) <= match.Settings.ai.repathDistance;
            if (!alreadyGoing)
            {
                Issue(match, brain, new MoveCommand(squad.Id, point, Angles.Of(direction)));
            }
        }

        private void KeepRetreating(Match match, Squad squad, SquadBrain brain)
        {
            if (!match.TryGetRetreatPoint(squad.Side, out float2 point))
            {
                return;
            }

            bool arrived = math.distance(squad.Position, point) <= match.Settings.squads.arrivalTolerance;
            bool alreadyGoing = squad.Order.Type == SquadOrderType.Move
                && math.distance(squad.Order.Target, point) <= match.Settings.ai.repathDistance;
            if (arrived || alreadyGoing)
            {
                return;
            }

            float facing = Angles.Of(point - squad.Position);
            Issue(match, brain, new MoveCommand(squad.Id, point, facing));
        }

        /// <summary>Ставит приказ с задержкой реакции; до его исполнения отряд новых приказов не получает.</summary>
        private static void Issue(Match match, SquadBrain brain, ICommand command)
        {
            int executeAt = match.CurrentTick + SecondsToTicks(match.Settings.ai.reactionDelay);
            match.Enqueue(executeAt, command);
            brain.BusyUntilTick = executeAt;
        }

        private static int SecondsToTicks(float seconds)
        {
            return (int)math.round(seconds * SimulationTime.TicksPerSecond);
        }
    }
}
