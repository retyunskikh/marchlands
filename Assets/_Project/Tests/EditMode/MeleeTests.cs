using System.Linq;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Melee;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class MeleeTests
    {
        private const int MaxTicks = 3000;

        private static int TicksFor(float seconds)
        {
            return (int)math.round(seconds * SimulationTime.TicksPerSecond);
        }

        /// <summary>Тики до начала рукопашной; падает, если она не началась.</summary>
        private static void RunUntilMelee(Match match, Squad squad)
        {
            for (int i = 0; i < MaxTicks && !squad.IsInMelee; i++)
            {
                match.Tick();
            }

            Assert.IsTrue(squad.IsInMelee, "Рукопашная не началась");
        }

        [Test]
        public void Reload_StandsStill_WhileInMelee()
        {
            Match match = CombatTestScenarios.FacingLines(1, 6f, out Squad attacker, out Squad defender);
            match.Enqueue(new ChargeCommand(attacker.Id, defender.Id));
            RunUntilMelee(match, attacker);
            attacker.FireState = FireState.Reloading;
            attacker.FireTimer = 10f;
            for (int i = 0; i < TicksFor(2f) && attacker.IsInMelee; i++)
            {
                match.Tick();
                Assert.AreEqual(10f, attacker.FireTimer, 1e-4f, "В драке не перезаряжают");
            }
        }

        [Test]
        public void Charge_RunsAtChargeSpeed()
        {
            Match match = CombatTestScenarios.FacingLines(1, 10f, out Squad attacker, out Squad defender);
            match.Enqueue(new ChargeCommand(attacker.Id, defender.Id));
            match.Tick();
            float before = attacker.Position.y;
            match.Tick();

            Assert.AreEqual(0.25f, attacker.Position.y - before, 1e-4f, "2.5 u/s × 0.1 s");
        }

        [Test]
        public void Charge_ShakenDefender_RoutsAtCheckDistance_WithoutMelee()
        {
            Match match = CombatTestScenarios.FacingLines(1, 10f, out Squad attacker, out Squad defender);
            defender.Morale = 45f;
            match.Enqueue(new ChargeCommand(attacker.Id, defender.Id));

            float distanceAtRout = -1f;
            for (int i = 0; i < 200 && distanceAtRout < 0f; i++)
            {
                match.Tick();
                if (defender.IsRouting)
                {
                    distanceAtRout = FireGeometry.Distance(attacker, defender, match.Settings.squads);
                }
            }

            Assert.That(distanceAtRout, Is.InRange(3.6f, 4.1f), "Защитник бежит на дистанции проверки");
            Assert.AreEqual(0, match.Engagements.Count);
            Assert.AreEqual(SquadOrderType.None, attacker.Order.Type, "Атакующий не преследует");
        }

        [Test]
        public void Charge_SteadyDefender_StartsFrontalMelee()
        {
            Match match = CombatTestScenarios.FacingLines(1, 10f, out Squad attacker, out Squad defender);
            match.Enqueue(new ChargeCommand(attacker.Id, defender.Id));

            RunUntilMelee(match, attacker);

            Assert.AreEqual(1, match.Engagements.Count);
            MeleeEngagement engagement = match.Engagements[0];
            Assert.AreSame(attacker, engagement.Attacker);
            Assert.AreSame(defender, engagement.Defender);
            Assert.IsFalse(engagement.IsFlank);
            Assert.IsTrue(defender.IsInMelee);
        }

        [Test]
        public void ChargingSquad_CanStillVolley()
        {
            var match = new Match(1, TestSettings.Create());
            Squad attacker = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            Squad defender = match.AddSquad(Side.Austria, new float2(0f, 8f), -90f, Formation.Line);
            match.Enqueue(new ChargeCommand(attacker.Id, defender.Id));
            match.Tick();

            Assert.AreEqual(SquadOrderType.Charge, attacker.Order.Type);
            Assert.IsTrue(VolleyRules.CanVolley(match, attacker), "Атака в штыки не мешает залпу на подходе");
        }

        [Test]
        public void Melee_AlwaysEnds()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                Match match = CombatTestScenarios.FacingLines(seed, 6f, out Squad attacker, out Squad defender);
                match.Enqueue(new ChargeCommand(attacker.Id, defender.Id));
                RunUntilMelee(match, attacker);

                int ticks = 0;
                while (match.Engagements.Count > 0)
                {
                    match.Tick();
                    ticks++;
                    Assert.Less(ticks, MaxTicks, "Рукопашная не закончилась за 300 с");
                }

                bool someoneBroke = attacker.IsRouting || defender.IsRouting
                    || match.FindSquad(attacker.Id) == null || match.FindSquad(defender.Id) == null;
                Assert.IsTrue(someoneBroke, "Рукопашная кончается бегством или гибелью одной из сторон");
            }
        }

        [Test]
        public void ChargeBonus_DefenderLosesMoreAtFirst()
        {
            Match match = CombatTestScenarios.FacingLines(1, 6f, out Squad attacker, out Squad defender);
            match.Enqueue(new ChargeCommand(attacker.Id, defender.Id));
            RunUntilMelee(match, attacker);

            for (int i = 0; i < TicksFor(3f) - 1; i++)
            {
                match.Tick();
            }

            // За 3 с: защитник 36 × 0.04 × 1.5 × 3 ≈ 6.5, атакующий 36 × 0.04 × 3 ≈ 4.3.
            Assert.AreEqual(36 - 6, defender.Alive, 1);
            Assert.AreEqual(36 - 4, attacker.Alive, 1);
        }

        [Test]
        public void FlankCharge_IsFlank_AndHurtsMore()
        {
            var match = new Match(1, TestSettings.Create());
            Squad defender = match.AddSquad(Side.Austria, new float2(0f, 10f), 90f, Formation.Line);
            Squad attacker = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            match.Enqueue(new ChargeCommand(attacker.Id, defender.Id));

            RunUntilMelee(match, attacker);
            Assert.IsTrue(match.Engagements[0].IsFlank, "Удар в тыл");

            for (int i = 0; i < TicksFor(2f) - 1; i++)
            {
                match.Tick();
            }

            // За 2 с: 36 × 0.04 × 1.5 (атака) × 1.5 (тыл) × 2 ≈ 6.5.
            Assert.AreEqual(36 - 6, defender.Alive, 1);
        }

        [Test]
        public void SquadsInMelee_DoNotMove_ButCanVolleyAtOpponent()
        {
            Match match = CombatTestScenarios.FacingLines(1, 6f, out Squad attacker, out Squad defender);
            match.Enqueue(new ChargeCommand(attacker.Id, defender.Id));
            RunUntilMelee(match, attacker);
            float2 position = attacker.Position;

            match.Enqueue(new MoveCommand(attacker.Id, new float2(0f, -20f), 90f));
            match.Enqueue(new VolleyCommand(defender.Id));
            match.Tick();
            match.Tick();

            Assert.AreEqual(SquadOrderType.None, attacker.Order.Type);
            Assert.Less(math.distance(position, attacker.Position), 1e-3f, "Приказ движения в рукопашной не выполняется");
            Assert.AreEqual(FireState.Aiming, defender.FireState, "Залп в упор по противнику в рукопашной");
            Assert.AreEqual(attacker.Id, defender.AimTargetId);
        }

        [Test]
        public void OutsiderDoesNotFireIntoMelee()
        {
            Match match = CombatTestScenarios.FacingLines(1, 6f, out Squad attacker, out Squad defender);
            match.Enqueue(new ChargeCommand(attacker.Id, defender.Id));
            RunUntilMelee(match, attacker);
            Squad outsider = match.AddSquad(attacker.Side, attacker.Position + new float2(0f, 12f), -90f, Formation.Line);

            Assert.IsFalse(VolleyRules.CanVolley(match, outsider), "Чужая свалка — не цель");
        }

        [Test]
        public void SquadFightingTwoEnemies_SplitsItsStrength()
        {
            float single = AttackerLossInFirstSecond(twoAttackers: false);
            float split = AttackerLossInFirstSecond(twoAttackers: true);

            // Один на один атакующий теряет ≈ 1.4 за секунду, вдвоём на одного — примерно вдвое меньше каждый.
            Assert.That(single, Is.InRange(1.2f, 1.5f));
            Assert.Less(split, single * 0.6f);
        }

        private static float AttackerLossInFirstSecond(bool twoAttackers)
        {
            var match = new Match(1, TestSettings.Create());
            Squad defender = match.AddSquad(Side.Austria, new float2(0f, 8f), -90f, Formation.Line);
            Squad first = match.AddSquad(Side.France, new float2(-2.5f, 0f), 90f, Formation.Column);
            match.Enqueue(new ChargeCommand(first.Id, defender.Id));
            if (twoAttackers)
            {
                Squad second = match.AddSquad(Side.France, new float2(2.5f, 0f), 90f, Formation.Column);
                match.Enqueue(new ChargeCommand(second.Id, defender.Id));
            }

            for (int i = 0; i < MaxTicks && defender.MeleeOpponents < (twoAttackers ? 2 : 1); i++)
            {
                match.Tick();
            }

            Assert.AreEqual(twoAttackers ? 2 : 1, defender.MeleeOpponents);
            int aliveBefore = first.Alive;
            float accumulatorBefore = first.MeleeLossAccumulator;
            for (int i = 0; i < TicksFor(1f); i++)
            {
                match.Tick();
            }

            return aliveBefore - first.Alive + first.MeleeLossAccumulator - accumulatorBefore;
        }

        [Test]
        public void LoadedDefender_GetsCounterVolleyOnChargingAttacker()
        {
            Match match = CombatTestScenarios.FacingLines(1, 11f, out Squad attacker, out Squad defender);
            match.Enqueue(new ChargeCommand(attacker.Id, defender.Id));

            bool counterVolley = false;
            for (int i = 0; i < MaxTicks && !attacker.IsInMelee && !attacker.IsRouting; i++)
            {
                // Игрок даёт встречный залп, как только атакующий в эффективной дальности.
                match.Enqueue(new VolleyCommand(defender.Id, match.Settings.combat.effectiveRange));
                match.Tick();
                counterVolley |= match.LastTickVolleys.Any(v => v.ShooterId == defender.Id && v.TargetId == attacker.Id);
            }

            Assert.IsTrue(counterVolley, "Заряженный защитник встречает атаку залпом");
            Assert.Less(attacker.Alive, 36);
        }

        [Test]
        public void ChargeIsCancelled_WhenTargetRoutsForOtherReasons()
        {
            Match match = CombatTestScenarios.FacingLines(1, 10f, out Squad attacker, out Squad defender);
            match.Enqueue(new ChargeCommand(attacker.Id, defender.Id));
            match.Tick();
            defender.Morale = 5f;
            match.Tick();
            match.Tick();

            Assert.AreEqual(SquadOrderType.None, attacker.Order.Type);
        }
    }
}
