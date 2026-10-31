using System.Collections.Generic;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Melee;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Morale
{
    /// <summary>
    /// Состояния морали в конце тика: бегство при низкой морали или численности меньше minSquadSize
    /// (с разовым штрафом союзникам рядом, который может вызвать цепное бегство, и разовым ободрением врагов рядом),
    /// восстановление в спокойствии. Отряды на грани ломаются по очереди, начиная с самого деморализованного:
    /// бегство первого ободряет его противников, и те могут устоять, даже если в том же тике тоже были на грани.
    /// Сбора в поле нет: добежавший до своих отряд уходит в резерв стороны.
    /// </summary>
    internal static class MoraleSystem
    {
        private static readonly List<Squad> Breaking = new List<Squad>();

        public static void Update(Match match, float deltaTime)
        {
            MoraleSettings settings = match.Settings.morale;
            ApplyRoutEffects(match);

            Breaking.Clear();
            foreach (Squad squad in match.Squads)
            {
                if (!squad.IsRouting && ShouldRout(match, squad))
                {
                    Breaking.Add(squad);
                }
            }

            Breaking.Sort((a, b) => a.Morale != b.Morale ? a.Morale.CompareTo(b.Morale) : a.Id.CompareTo(b.Id));
            foreach (Squad squad in Breaking)
            {
                // Бегство предыдущих могло ободрить этот отряд — проверяем заново.
                if (!squad.IsRouting && ShouldRout(match, squad))
                {
                    Rout(match, squad);
                    ApplyRoutEffects(match);
                }
            }

            Breaking.Clear();

            foreach (Squad squad in match.Squads)
            {
                if (squad.IsRouting)
                {
                    TryWithdraw(match, squad);
                    continue;
                }

                squad.TimeSinceLoss += deltaTime;
                // В спокойствии дух восстанавливается быстро, в бою — медленно, но всегда.
                float regen = IsCalm(match, squad) ? math.max(settings.moraleRegen, settings.moraleBaseRegen) : settings.moraleBaseRegen;
                squad.Morale = math.min(MoraleMath.MaxMorale, squad.Morale + regen * deltaTime);
            }
        }

        private static bool ShouldRout(Match match, Squad squad)
        {
            // Толпа рекрутов не бежит: её численность — это резерв.
            return !squad.IsCamp && squad.Morale < match.Settings.morale.routThreshold || squad.Alive < UnitStats.Of(squad, match.Settings).minSquadSize;
        }

        /// <summary>Бегущий, добежавший до казармы (штаба, точки отхода), уходит с поля в резерв стороны.</summary>
        private static void TryWithdraw(Match match, Squad squad)
        {
            if (squad.Alive <= 0 || !match.TryGetRetreatPoint(squad, out float2 point))
            {
                return;
            }

            if (math.distance(squad.Position, point) <= match.Settings.squads.arrivalTolerance)
            {
                // Разбитый расчёт пушки, добежав до штаба, просто исчезает: без пушки он не отряд.
                if (!squad.IsArtillery)
                {
                    match.GetPlayer(squad.Side).AddReserve(squad.Type, squad.Alive);
                }

                squad.IsWithdrawn = true;
            }
        }

        /// <summary>Обращает отряд в бегство: приказ, прицеливание, перестроение и рукопашная прерываются.</summary>
        public static void Rout(Match match, Squad squad)
        {
            if (squad.IsRouting)
            {
                return;
            }

            squad.MoraleState = MoraleState.Routing;
            squad.Order = SquadOrder.None;
            // Марш забыт: бегущий прокладывает свой путь — к своим.
            squad.ClearRoute();
            squad.ChargeChecked = false;
            FireSystem.CancelAiming(squad);
            SquadMovementSystem.CancelFormationChange(squad);
            MeleeSystem.Disengage(match, squad);
            match.PendingRouts.Enqueue(squad);
        }

        /// <summary>
        /// Каждый новый беглец один раз бьёт по морали союзников рядом (это может обратить в бегство и их)
        /// и один раз ободряет врагов рядом.
        /// </summary>
        private static void ApplyRoutEffects(Match match)
        {
            MoraleSettings settings = match.Settings.morale;
            while (match.PendingRouts.Count > 0)
            {
                Squad routed = match.PendingRouts.Dequeue();
                foreach (Squad other in match.Squads)
                {
                    if (other == routed || other.IsWithdrawn || other.IsCamp || other.IsRouting && other.Side != routed.Side)
                    {
                        continue;
                    }

                    float distance = math.distance(other.Position, routed.Position);
                    if (other.Side != routed.Side)
                    {
                        if (distance <= settings.enemyRoutRadius)
                        {
                            other.Morale = math.min(MoraleMath.MaxMorale, other.Morale + settings.enemyRoutBonus);
                        }

                        continue;
                    }

                    if (distance > settings.allyRoutRadius)
                    {
                        continue;
                    }

                    other.Morale = math.max(0f, other.Morale - settings.allyRoutPenalty);
                    if (!other.IsRouting && other.Morale < settings.routThreshold)
                    {
                        Rout(match, other);
                    }
                }
            }
        }

        /// <summary>Спокойствие: moraleCalmTime без потерь, нет рукопашной и врагов ближе calmEnemyRadius.</summary>
        private static bool IsCalm(Match match, Squad squad)
        {
            MoraleSettings settings = match.Settings.morale;
            if (squad.TimeSinceLoss < settings.moraleCalmTime || squad.IsInMelee)
            {
                return false;
            }

            foreach (Squad other in match.Squads)
            {
                if (other.Side != squad.Side && math.distance(other.Position, squad.Position) <= settings.calmEnemyRadius)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
