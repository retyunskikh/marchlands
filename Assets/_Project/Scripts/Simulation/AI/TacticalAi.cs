using System.Collections.Generic;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Melee;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.AI
{
    /// <summary>
    /// Тактический ИИ одной стороны (этап 4 docs/design.md). Для каждого отряда: наступать на ближайшего
    /// не бегущего врага, держа огонь до случайной дистанции между pointBlankRange и effectiveRange →
    /// залп → штыки, если враг перезаряжается рядом, а своя мораль высока → отход при низкой морали.
    /// Отряд без врагов ближе aiEngageRadius свободен и идёт захватывать ближайшую ничью или вражескую деревню;
    /// если захватывать нечего, наступает на ближайшего врага. На врага у своего штаба или деревни (угроза) идёт
    /// ближайший свободный отряд. Приказы отдаёт только командами, с задержкой реакции.
    /// </summary>
    public sealed class TacticalAi : IMatchController
    {
        private readonly Dictionary<int, SquadBrain> brains = new Dictionary<int, SquadBrain>();
        private readonly Dictionary<int, int> defenders = new Dictionary<int, int>();
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
            public int VillageId;

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
            AssignDefenders(match);

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

            Squad target = defenders.TryGetValue(squad.Id, out int threatId)
                ? ForceTarget(match, brain, match.FindSquad(threatId))
                : SelectTarget(match, squad, brain, ai.engageRadius);
            if (target == null)
            {
                Village village = SelectVillage(match, squad, brain);
                if (village != null)
                {
                    GoToVillage(match, squad, village, brain);
                    return;
                }

                target = SelectTarget(match, squad, brain, float.MaxValue);
            }

            brain.VillageId = 0;
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

        /// <summary>
        /// Держит текущую цель, пока она жива, не бежит и ближе <paramref name="radius"/>; иначе берёт ближайшего
        /// не бегущего врага в этом радиусе. Дистанция огня выбирается заново только при смене цели.
        /// </summary>
        private static Squad SelectTarget(Match match, Squad squad, SquadBrain brain, float radius)
        {
            Squad current = match.FindSquad(brain.TargetId);
            if (current != null && !current.IsRouting && math.distance(current.Position, squad.Position) <= radius)
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

                float distance = math.distance(other.Position, squad.Position);
                if (distance <= radius && distance < bestDistance)
                {
                    best = other;
                    bestDistance = distance;
                }
            }

            if (best == null)
            {
                return null;
            }

            bool changed = best.Id != brain.TargetId;
            brain.TargetId = best.Id;
            if (changed)
            {
                brain.FireDistance = RollFireDistance(match);
            }

            return best;
        }

        /// <summary>
        /// Угроза — не бегущий враг ближе aiDefenseRadius к своему штабу или своей деревне. На каждую угрозу
        /// назначается ближайший свободный отряд: не связанный боем с другим врагом, не отходящий и выполняющий приказы.
        /// </summary>
        private void AssignDefenders(Match match)
        {
            defenders.Clear();
            foreach (Squad threat in match.Squads)
            {
                if (threat.Side == Side || threat.IsRouting || !IsThreat(match, threat))
                {
                    continue;
                }

                Squad best = null;
                float bestDistance = float.MaxValue;
                foreach (Squad own in match.Squads)
                {
                    if (own.Side != Side || !own.CanReceiveOrders || defenders.ContainsKey(own.Id)
                        || (brains.TryGetValue(own.Id, out SquadBrain brain) && brain.Stance == Stance.Retreat)
                        || IsEngagedWithOther(match, own, threat))
                    {
                        continue;
                    }

                    float distance = math.distancesq(own.Position, threat.Position);
                    if (distance < bestDistance)
                    {
                        best = own;
                        bestDistance = distance;
                    }
                }

                if (best != null)
                {
                    defenders.Add(best.Id, threat.Id);
                }
            }
        }

        private bool IsThreat(Match match, Squad enemy)
        {
            float radius = match.Settings.ai.defenseRadius;
            Building headquarters = match.GetHeadquarters(Side);
            if (headquarters != null && math.distance(enemy.Position, headquarters.Position) <= radius)
            {
                return true;
            }

            foreach (Village village in match.Villages)
            {
                if (village.Owner == Side && math.distance(enemy.Position, village.Position) <= radius)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Есть ли у отряда в радиусе боя другой не бегущий враг, кроме <paramref name="threat"/>.</summary>
        private static bool IsEngagedWithOther(Match match, Squad own, Squad threat)
        {
            foreach (Squad other in match.Squads)
            {
                if (other != threat && other.Side != own.Side && !other.IsRouting
                    && math.distance(other.Position, own.Position) <= match.Settings.ai.engageRadius)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Делает <paramref name="target"/> целью отряда; дистанция огня выбирается заново при смене цели.</summary>
        private static Squad ForceTarget(Match match, SquadBrain brain, Squad target)
        {
            if (target == null)
            {
                return null;
            }

            if (target.Id != brain.TargetId)
            {
                brain.TargetId = target.Id;
                brain.FireDistance = RollFireDistance(match);
            }

            return target;
        }

        private static float RollFireDistance(Match match)
        {
            // Верхняя граница чуть меньше effectiveRange: с допуском подхода залп всё равно в эффективной дальности.
            CombatSettings combat = match.Settings.combat;
            float maxDistance = combat.effectiveRange - match.Settings.squads.arrivalTolerance;
            return match.Random.Range(combat.pointBlankRange, maxDistance);
        }

        /// <summary>
        /// Ближайшая ничья или вражеская деревня. Деревни, за которыми уже идут другие отряды этого ИИ,
        /// берутся, только если других нет. null — захватывать нечего.
        /// </summary>
        private Village SelectVillage(Match match, Squad squad, SquadBrain brain)
        {
            Village current = FindVillage(match, brain.VillageId);
            if (current != null && current.Owner != Side)
            {
                return current;
            }

            Village best = null;
            float bestDistance = float.MaxValue;
            bool bestReserved = true;
            foreach (Village village in match.Villages)
            {
                if (village.Owner == Side)
                {
                    continue;
                }

                bool reserved = IsReservedByOther(match, squad, village);
                float distance = math.distancesq(village.Position, squad.Position);
                bool better = best == null
                    || (bestReserved && !reserved)
                    || (reserved == bestReserved && distance < bestDistance);
                if (better)
                {
                    best = village;
                    bestDistance = distance;
                    bestReserved = reserved;
                }
            }

            brain.VillageId = best?.Id ?? 0;
            return best;
        }

        private bool IsReservedByOther(Match match, Squad squad, Village village)
        {
            foreach (Squad other in match.Squads)
            {
                if (other != squad && other.Side == Side
                    && brains.TryGetValue(other.Id, out SquadBrain otherBrain) && otherBrain.VillageId == village.Id)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Идёт к центру деревни и стоит там, пока она не станет своей.</summary>
        private static void GoToVillage(Match match, Squad squad, Village village, SquadBrain brain)
        {
            float distance = math.distance(squad.Position, village.Position);
            bool alreadyGoing = squad.Order.Type == SquadOrderType.Move
                && math.distance(squad.Order.Target, village.Position) <= match.Settings.ai.repathDistance;
            bool standingInside = squad.Order.Type == SquadOrderType.None && distance <= match.Settings.economy.captureRadius;
            if (alreadyGoing || standingInside)
            {
                return;
            }

            float2 toVillage = village.Position - squad.Position;
            float facing = math.lengthsq(toVillage) > 0f ? Angles.Of(toVillage) : squad.Facing;
            Issue(match, brain, new MoveCommand(squad.Id, village.Position, facing));
        }

        private static Village FindVillage(Match match, int id)
        {
            foreach (Village village in match.Villages)
            {
                if (village.Id == id)
                {
                    return village;
                }
            }

            return null;
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
