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
    /// начало рукопашной при касании атакующего, а также при любом сближении врагов ближе contactDistance,
    /// и потери сторон. Потери тика считаются по численности на его начало,
    /// поэтому результат не зависит от порядка пар.
    /// </summary>
    internal static class MeleeSystem
    {
        public static void Update(Match match, float deltaTime)
        {
            UpdateCharges(match, deltaTime);
            EngageContacts(match);
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
                    Engage(match, attacker, target, isCharge: true);
                    continue;
                }

                // Атака, упёршаяся в другого врага, превращается в рукопашную с ним.
                Squad blocker = FindEnemyInContact(match, attacker, contactDistance);
                if (blocker != null)
                {
                    Engage(match, attacker, blocker, isCharge: true);
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

        /// <summary>
        /// Враги, сошедшиеся ближе contactDistance, начинают рукопашную. Атакующий — бегущий в атаку (с бонусом удара),
        /// а если в атаку не бежит никто — тот, кто сдвинулся за тик, при равенстве — с меньшим номером.
        /// </summary>
        private static void EngageContacts(Match match)
        {
            float gap = match.Settings.melee.contactDistance;
            for (int i = 0; i < match.Squads.Count; i++)
            {
                Squad a = match.Squads[i];
                if (!CanFight(a))
                {
                    continue;
                }

                for (int j = i + 1; j < match.Squads.Count; j++)
                {
                    Squad b = match.Squads[j];
                    if (b.Side == a.Side || !CanFight(b) || AreEngaged(match, a, b))
                    {
                        continue;
                    }

                    SquadFootprint fa = SquadFootprint.Of(a, match.Settings.squads).Inflated(gap * 0.5f);
                    SquadFootprint fb = SquadFootprint.Of(b, match.Settings.squads).Inflated(gap * 0.5f);
                    if (!SquadFootprint.TryGetPenetration(fa, fb, out _))
                    {
                        continue;
                    }

                    // Кто бежит в атаку — тот и атакует, с бонусом удара; иначе атакует тот, кто шёл на врага.
                    bool aCharges = a.Order.Type == SquadOrderType.Charge;
                    bool bCharges = b.Order.Type == SquadOrderType.Charge;
                    float movedA = math.distancesq(a.PreviousPosition, a.Position);
                    float movedB = math.distancesq(b.PreviousPosition, b.Position);
                    bool aAttacks = aCharges != bCharges ? aCharges : movedA > movedB || (movedA == movedB && a.Id < b.Id);
                    Squad attacker = aAttacks ? a : b;
                    Engage(match, attacker, aAttacks ? b : a, isCharge: attacker.Order.Type == SquadOrderType.Charge);
                }
            }
        }

        private static bool CanFight(Squad squad)
        {
            return squad.Alive > 0 && !squad.IsRouting && !squad.IsWithdrawn;
        }

        private static bool AreEngaged(Match match, Squad a, Squad b)
        {
            foreach (MeleeEngagement existing in match.Engagements)
            {
                if (existing.Involves(a) && existing.Involves(b))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Engage(Match match, Squad attacker, Squad defender, bool isCharge)
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
            match.AddEngagement(new MeleeEngagement(attacker, defender, match.CurrentTick, isFlank, isCharge));
            attacker.Order = SquadOrder.None;
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
                float attackerMultiplier = engagement.IsCharge && elapsed < melee.chargeBonusDuration ? chargeBonus : 1f;
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
