using Marchlands.Simulation.AI;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class EconomyTests
    {
        private static int TicksFor(float seconds)
        {
            return (int)math.round(seconds * SimulationTime.TicksPerSecond);
        }

        private static void RunTicks(Match match, int count)
        {
            for (int i = 0; i < count; i++)
            {
                match.Tick();
            }
        }

        private static int TicksUntilOwned(Match match, Village village, Side side)
        {
            int ticks = 0;
            while (village.Owner != side)
            {
                match.Tick();
                ticks++;
                Assert.Less(ticks, TicksFor(60f), "Деревня так и не захвачена");
            }

            return ticks;
        }

        [Test]
        public void Squad_CapturesNeutralVillage_InCaptureTime()
        {
            var match = new Match(1, TestSettings.Create());
            Village village = match.AddVillage(float2.zero);
            match.AddSquad(Side.France, new float2(0f, 2f), 90f, Formation.Line);

            int ticks = TicksUntilOwned(match, village, Side.France);

            Assert.That(ticks, Is.InRange(TicksFor(15f) - 1, TicksFor(15f) + 1));
        }

        [Test]
        public void EnemyVillage_GoesStraightToNewOwner_InCaptureTime()
        {
            var match = new Match(1, TestSettings.Create());
            Village village = match.AddVillage(float2.zero);
            village.Owner = Side.Austria;
            match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);

            int ticks = TicksUntilOwned(match, village, Side.France);

            Assert.That(ticks, Is.InRange(TicksFor(15f) - 1, TicksFor(15f) + 1));
        }

        [Test]
        public void SquadOutsideRadius_DoesNotCapture()
        {
            var match = new Match(1, TestSettings.Create());
            Village village = match.AddVillage(float2.zero);
            match.AddSquad(Side.France, new float2(0f, 4.5f), 90f, Formation.Line);

            RunTicks(match, TicksFor(20f));

            Assert.IsNull(village.Owner);
            Assert.AreEqual(0f, village.CaptureProgress);
        }

        [Test]
        public void ContestedVillage_ProgressFreezes()
        {
            var match = new Match(1, TestSettings.Create());
            Village village = match.AddVillage(float2.zero);
            Squad french = match.AddSquad(Side.France, new float2(-3f, 0f), 90f, Formation.Column);
            RunTicks(match, TicksFor(5f));
            float before = village.CaptureProgress;
            Assert.Greater(before, 4.5f);

            match.AddSquad(Side.Austria, new float2(3f, 0f), 90f, Formation.Column);
            RunTicks(match, TicksFor(20f));

            Assert.AreEqual(before, village.CaptureProgress, 1e-4f);
            Assert.IsNull(village.Owner);
            Assert.AreEqual(Side.France, village.CapturingSide);
            Assert.IsTrue(village.IsContested, "Деревня помечена как оспариваемая");
            Assert.IsNotNull(match.FindSquad(french.Id));
        }

        [Test]
        public void RoutingSquad_DoesNotCapture_NorBlock()
        {
            var match = new Match(1, TestSettings.Create());
            Village village = match.AddVillage(float2.zero);
            Squad routing = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            routing.Morale = 5f;
            match.Tick();
            Assert.IsTrue(routing.IsRouting);
            Assert.AreEqual(0f, village.CaptureProgress, "Бегущий не захватывает");

            var second = new Match(1, TestSettings.Create());
            Village contested = second.AddVillage(float2.zero);
            Squad capturer = second.AddSquad(Side.Austria, new float2(-3f, 0f), 90f, Formation.Column);
            Squad fleeing = second.AddSquad(Side.France, new float2(3f, 0f), 90f, Formation.Column);
            fleeing.Morale = 5f;

            int ticks = TicksUntilOwned(second, contested, Side.Austria);

            Assert.That(ticks, Is.InRange(TicksFor(15f) - 1, TicksFor(15f) + 1), "Бегущий враг захвату не мешает");
            Assert.IsNotNull(second.FindSquad(capturer.Id));
        }

        [Test]
        public void UnfinishedCapture_DecaysWhenZoneIsEmpty()
        {
            var match = new Match(1, TestSettings.Create());
            Village village = match.AddVillage(float2.zero);
            Squad squad = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            RunTicks(match, TicksFor(6f));
            Assert.AreEqual(6f, village.CaptureProgress, 0.15f);

            // Отряд сразу вне зоны: прогресс убывает по 1 с в секунду.
            squad.Position = new float2(0f, 30f);
            RunTicks(match, TicksFor(4f));
            Assert.AreEqual(2f, village.CaptureProgress, 0.15f);

            RunTicks(match, TicksFor(3f));
            Assert.AreEqual(0f, village.CaptureProgress);
            Assert.IsNull(village.CapturingSide);
        }

        [Test]
        public void NewCapturer_StartsFromZero()
        {
            var match = new Match(1, TestSettings.Create());
            Village village = match.AddVillage(float2.zero);
            Squad french = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            RunTicks(match, TicksFor(10f));
            french.Position = new float2(0f, 30f);

            match.AddSquad(Side.Austria, float2.zero, 90f, Formation.Line);
            match.Tick();

            Assert.AreEqual(Side.Austria, village.CapturingSide);
            Assert.AreEqual(SimulationTime.TickDuration, village.CaptureProgress, 1e-4f);
        }

        [Test]
        public void Income_IsPaidEveryInterval_PerOwnedVillage()
        {
            var match = new Match(1, TestSettings.Create());
            match.AddVillage(new float2(-20f, 0f)).Owner = Side.France;
            match.AddVillage(new float2(0f, 0f)).Owner = Side.France;
            match.AddVillage(new float2(20f, 0f)).Owner = Side.Austria;
            PlayerState france = match.GetPlayer(Side.France);
            PlayerState austria = match.GetPlayer(Side.Austria);
            Assert.AreEqual(150, france.Gold);

            RunTicks(match, TicksFor(5f) - 1);
            Assert.AreEqual(150, france.Gold, "До конца первого интервала дохода нет");

            match.Tick();
            Assert.AreEqual(156, france.Gold);
            Assert.AreEqual(153, austria.Gold);

            RunTicks(match, TicksFor(10f));
            Assert.AreEqual(168, france.Gold);
        }

        [Test]
        public void FreeAiSquad_CapturesNearestVillage()
        {
            var match = new Match(1, TestSettings.Create());
            Village near = match.AddVillage(new float2(0f, -8f));
            match.AddVillage(new float2(0f, -30f));
            match.AddSquad(Side.Austria, float2.zero, -90f, Formation.Line);
            match.AddController(new TacticalAi(Side.Austria));

            int ticks = TicksUntilOwned(match, near, Side.Austria);

            Assert.Less(ticks, TicksFor(30f));
        }

        [Test]
        public void TwoFreeAiSquads_SplitBetweenVillages()
        {
            var match = new Match(1, TestSettings.Create());
            Village left = match.AddVillage(new float2(-10f, -10f));
            Village right = match.AddVillage(new float2(10f, -10f));
            match.AddSquad(Side.Austria, new float2(-1f, 0f), -90f, Formation.Column);
            match.AddSquad(Side.Austria, new float2(1f, 0f), -90f, Formation.Column);
            match.AddController(new TacticalAi(Side.Austria));

            RunTicks(match, TicksFor(40f));

            Assert.AreEqual(Side.Austria, left.Owner);
            Assert.AreEqual(Side.Austria, right.Owner);
        }

        [Test]
        public void AiSquad_FightsEnemyWithinEngageRadius_InsteadOfCapturing()
        {
            var match = new Match(1, TestSettings.Create());
            Village village = match.AddVillage(new float2(0f, 30f));
            Squad ai = match.AddSquad(Side.Austria, float2.zero, -90f, Formation.Line);
            match.AddSquad(Side.France, new float2(0f, -12f), 90f, Formation.Line);
            match.AddController(new TacticalAi(Side.Austria));

            RunTicks(match, TicksFor(2f));

            Assert.AreEqual(SquadOrderType.Move, ai.Order.Type);
            Assert.Less(ai.Order.Target.y, 0f, "Идёт на врага, а не к деревне");
            Assert.IsNull(village.Owner);
        }

        [Test]
        public void AiWithNothingToCapture_AttacksDistantEnemy()
        {
            var match = new Match(1, TestSettings.Create());
            match.AddVillage(new float2(0f, 5f)).Owner = Side.Austria;
            Squad ai = match.AddSquad(Side.Austria, float2.zero, -90f, Formation.Line);
            match.AddSquad(Side.France, new float2(0f, -30f), 90f, Formation.Line);
            match.AddController(new TacticalAi(Side.Austria));

            RunTicks(match, TicksFor(2f));

            Assert.AreEqual(SquadOrderType.Move, ai.Order.Type);
            Assert.Less(ai.Order.Target.y, -10f);
        }
    }
}
