using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Melee;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Morale
{
    /// <summary>
    /// Состояния морали в конце тика: бегство (с разовым штрафом союзникам рядом, который может вызвать
    /// цепное бегство), восстановление в спокойствии, сбор бегущих и распад малых бегущих отрядов.
    /// </summary>
    internal static class MoraleSystem
    {
        public static void Update(Match match, float deltaTime)
        {
            MoraleSettings settings = match.Settings.morale;

            foreach (Squad squad in match.Squads)
            {
                if (!squad.IsRouting && squad.Morale < settings.routThreshold)
                {
                    Rout(match, squad);
                }
            }

            ApplyAllyRoutPenalties(match);

            foreach (Squad squad in match.Squads)
            {
                squad.TimeSinceLoss += deltaTime;
                if (IsCalm(match, squad))
                {
                    squad.Morale = math.min(MoraleMath.MaxMorale, squad.Morale + settings.moraleRegen * deltaTime);
                }

                if (squad.IsRouting && squad.Morale >= settings.rallyThreshold)
                {
                    squad.MoraleState = MoraleState.Steady;
                    squad.Order = SquadOrder.None;
                }

                if (squad.IsRouting && squad.Alive <= settings.disbandSize)
                {
                    squad.IsDisbanded = true;
                }
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
            squad.ChargeChecked = false;
            FireSystem.CancelAiming(squad);
            SquadMovementSystem.CancelFormationChange(squad);
            MeleeSystem.Disengage(match, squad);
            match.PendingRouts.Enqueue(squad);
        }

        /// <summary>Каждый новый беглец один раз бьёт по морали союзников рядом; это может обратить в бегство и их.</summary>
        private static void ApplyAllyRoutPenalties(Match match)
        {
            MoraleSettings settings = match.Settings.morale;
            while (match.PendingRouts.Count > 0)
            {
                Squad routed = match.PendingRouts.Dequeue();
                foreach (Squad ally in match.Squads)
                {
                    if (ally == routed || ally.Side != routed.Side || ally.IsDisbanded)
                    {
                        continue;
                    }

                    if (math.distance(ally.Position, routed.Position) > settings.allyRoutRadius)
                    {
                        continue;
                    }

                    ally.Morale = math.max(0f, ally.Morale - settings.allyRoutPenalty);
                    if (!ally.IsRouting && ally.Morale < settings.routThreshold)
                    {
                        Rout(match, ally);
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
