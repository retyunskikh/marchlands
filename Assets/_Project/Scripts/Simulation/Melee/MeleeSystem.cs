using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Morale;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Melee
{
    /// <summary>
    /// Штыковая атака и рукопашная (раздел 3.4 docs/design.md): проверка морали защитника на подходе,
    /// начало рукопашной при касании и потери сторон. Потери тика считаются по численности на его начало,
    /// поэтому результат не зависит от порядка пар.
    /// </summary>
    internal static class MeleeSystem
    {
        public static void Update(Match match, float deltaTime)
        {
            UpdateCharges(match, deltaTime);
            ApplyMeleeLosses(match, deltaTime);
        }

        /// <summary>Убирает рукопашные, где одна из сторон бежит, погибла или распалась.</summary>
        public static void RemoveFinished(Match match)
        {
            for (int i = match.Engagements.Count - 1; i >= 0; i--)
            {
                MeleeEngagement engagement = match.Engagements[i];
                if (IsOut(engagement.Attacker) || IsOut(engagement.Defender))
                {
                    RemoveAt(match, i);
                }
            }
        }

        /// <summary>Выводит отряд из всех его рукопашных.</summary>
        public static void Disengage(Match match, Squad squad)
        {
            for (int i = match.Engagements.Count - 1; i >= 0; i--)
            {
                if (match.Engagements[i].Involves(squad))
                {
                    RemoveAt(match, i);
                }
            }
        }

        private static void UpdateCharges(Match match, float deltaTime)
        {
            MeleeSettings melee = match.Settings.melee;
            float contactDistance = match.Settings.squads.arrivalTolerance;

            foreach (Squad attacker in match.Squads)
            {
                if (attacker.Order.Type != SquadOrderType.Charge || !attacker.CanReceiveOrders)
                {
                    continue;
                }

                Squad target = match.FindSquad(attacker.Order.TargetSquadId);
                if (!ChargeRules.IsValidTarget(attacker, target))
                {
                    StopCharge(attacker);
                    continue;
                }

                float distance = FireGeometry.Distance(attacker, target, match.Settings.squads);
                if (!attacker.ChargeChecked && distance <= melee.chargeCheckDistance)
                {
                    attacker.ChargeChecked = true;
                    if (target.Morale < melee.chargeMoraleThreshold)
                    {
                        // Защитник не принимает боя; атакующий останавливается, а не преследует.
                        MoraleSystem.Rout(match, target);
                        StopCharge(attacker);
                        continue;
                    }
                }

                if (distance <= contactDistance)
                {
                    Engage(match, attacker, target);
                    continue;
                }

                // Атака, упёршаяся в другого врага, превращается в рукопашную с ним.
                Squad blocker = FindEnemyInContact(match, attacker, contactDistance);
                if (blocker != null)
                {
                    Engage(match, attacker, blocker);
                    continue;
                }

                attacker.ChargeTime += deltaTime;
                if (attacker.ChargeTime >= melee.chargeMaxDuration)
                {
                    // Путь перекрыт (например, своим отрядом) — атака выдыхается.
                    StopCharge(attacker);
                }
            }
        }

        private static Squad FindEnemyInContact(Match match, Squad attacker, float contactDistance)
        {
            foreach (Squad other in match.Squads)
            {
                if (ChargeRules.IsValidTarget(attacker, other)
                    && FireGeometry.Distance(attacker, other, match.Settings.squads) <= contactDistance)
                {
                    return other;
                }
            }

            return null;
        }

        private static void Engage(Match match, Squad attacker, Squad defender)
        {
            StopCharge(attacker);
            foreach (MeleeEngagement existing in match.Engagements)
            {
                if (existing.Involves(attacker) && existing.Involves(defender))
                {
                    return;
                }
            }

            bool isFlank = MoraleMath.IsFlank(defender, attacker.Position, match.Settings.morale);
            match.AddEngagement(new MeleeEngagement(attacker, defender, match.CurrentTick, isFlank));
            attacker.MeleeOpponents++;
            defender.MeleeOpponents++;

            // Рукопашная прерывает всё, чем был занят защитник.
            defender.Order = SquadOrder.None;
            FireSystem.CancelAiming(defender);
            FireSystem.CancelAiming(attacker);
            SquadMovementSystem.CancelFormationChange(defender);
        }

        private static void ApplyMeleeLosses(Match match, float deltaTime)
        {
            MeleeSettings melee = match.Settings.melee;
            float elapsedPerTick = SimulationTime.TickDuration;

            // Сила отряда делится поровну между всеми его противниками.
            foreach (MeleeEngagement engagement in match.Engagements)
            {
                Squad attacker = engagement.Attacker;
                Squad defender = engagement.Defender;
                float attackerStrength = (float)attacker.Alive / attacker.MeleeOpponents;
                float defenderStrength = (float)defender.Alive / defender.MeleeOpponents;

                float elapsed = (match.CurrentTick - engagement.StartTick) * elapsedPerTick;
                // Бонус атаки (сверх 1) у конницы больше: удар в сабли с разгона.
                float chargeBonus = 1f + (melee.chargeBonus - 1f) * UnitStats.Of(attacker, match.Settings).chargeBonusMultiplier;
                float attackerMultiplier = elapsed < melee.chargeBonusDuration ? chargeBonus : 1f;
                if (engagement.IsFlank)
                {
                    attackerMultiplier *= melee.flankMeleeMultiplier;
                }

                attackerMultiplier *= UnitStats.Of(attacker, match.Settings).meleeMultiplier;
                float defenderMultiplier = UnitStats.Of(defender, match.Settings).meleeMultiplier;
                defender.MeleeLossAccumulator += attackerStrength * melee.meleeKillRate * attackerMultiplier * deltaTime;
                attacker.MeleeLossAccumulator += defenderStrength * melee.meleeKillRate * defenderMultiplier * deltaTime;
            }

            foreach (Squad squad in match.Squads)
            {
                if (!squad.IsInMelee)
                {
                    continue;
                }

                int casualties = (int)math.floor(squad.MeleeLossAccumulator);
                if (casualties <= 0)
                {
                    continue;
                }

                squad.MeleeLossAccumulator -= casualties;
                int aliveBefore = squad.Alive;
                casualties = math.min(casualties, aliveBefore);
                squad.Alive = aliveBefore - casualties;
                MoraleMath.ApplyLoss(squad, casualties, aliveBefore, false, IsHitInFlank(match, squad), match.Settings);
            }
        }

        private static bool IsHitInFlank(Match match, Squad squad)
        {
            foreach (MeleeEngagement engagement in match.Engagements)
            {
                if (engagement.Defender == squad && engagement.IsFlank)
                {
                    return true;
                }
            }

            return false;
        }

        private static void StopCharge(Squad attacker)
        {
            attacker.Order = SquadOrder.None;
            attacker.ChargeChecked = false;
            attacker.ChargeTime = 0f;
        }

        private static bool IsOut(Squad squad)
        {
            return squad.IsRouting || squad.Alive <= 0 || squad.IsWithdrawn;
        }

        private static void RemoveAt(Match match, int index)
        {
            MeleeEngagement engagement = match.Engagements[index];
            engagement.Attacker.MeleeOpponents--;
            engagement.Defender.MeleeOpponents--;
            if (!engagement.Attacker.IsInMelee)
            {
                engagement.Attacker.MeleeLossAccumulator = 0f;
            }

            if (!engagement.Defender.IsInMelee)
            {
                engagement.Defender.MeleeLossAccumulator = 0f;
            }

            match.RemoveEngagementAt(index);
        }
    }
}
