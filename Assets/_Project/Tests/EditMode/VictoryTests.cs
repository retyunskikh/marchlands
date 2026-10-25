using System.Linq;
using Marchlands.Simulation.AI;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using Marchlands.Simulation.Victory;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class VictoryTests
    {
        private static readonly float2 FranceHq = new float2(-52f, 0f);
        private static readonly float2 AustriaHq = new float2(52f, 0f);

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

        /// <summary>Штабы по краям, у обеих сторон по готовой казарме с золотом — чтобы не сработало уничтожение.</summary>
        private static Match CreateMatch()
        {
            var match = new Match(1, TestSettings.Create());
            match.EnableVictoryConditions();
            match.AddHeadquarters(Side.France, FranceHq);
            match.AddHeadquarters(Side.Austria, AustriaHq);
            match.AddBarracks(Side.France, FranceHq + new float2(6f, 0f)).BuildRemaining = 0f;
            match.AddBarracks(Side.Austria, AustriaHq - new float2(6f, 0f)).BuildRemaining = 0f;
            return match;
        }

        private static int TicksUntilResult(Match match, float limitSeconds)
        {
            int ticks = 0;
            while (match.Result == null)
            {
                match.Tick();
                ticks++;
                Assert.Less(ticks, TicksFor(limitSeconds), "Матч не закончился");
            }

            return ticks;
        }

        [Test]
        public void HeadquartersCapture_WinsAfterCaptureTime()
        {
            Match match = CreateMatch();
            match.AddSquad(Side.France, AustriaHq + new float2(-3f, 0f), 0f, Formation.Column);
            match.AddSquad(Side.Austria, new float2(0f, 30f), 180f, Formation.Line);

            int ticks = TicksUntilResult(match, 60f);

            Assert.That(ticks, Is.InRange(TicksFor(30f) - 1, TicksFor(30f) + 1));
            Assert.AreEqual(Side.France, match.Result.Winner);
            Assert.AreEqual(VictoryReason.HeadquartersCaptured, match.Result.Reason);
        }

        [Test]
        public void HeadquartersCapture_FreezesWhileDefended()
        {
            Match match = CreateMatch();
            match.AddSquad(Side.France, AustriaHq + new float2(-3f, 3f), 0f, Formation.Column);
            match.AddSquad(Side.Austria, AustriaHq + new float2(1f, -3f), 180f, Formation.Column);

            RunTicks(match, TicksFor(60f));

            Assert.IsNull(match.Result);
            Assert.AreEqual(0f, match.GetHeadquarters(Side.Austria).CaptureProgress);
        }

        [Test]
        public void RoutingSquad_DoesNotCaptureHeadquarters()
        {
            Match match = CreateMatch();
            Squad squad = match.AddSquad(Side.France, AustriaHq + new float2(-3f, 0f), 0f, Formation.Column);
            match.AddSquad(Side.Austria, new float2(0f, 30f), 180f, Formation.Line);
            squad.Morale = 5f;

            RunTicks(match, TicksFor(5f));

            Assert.AreEqual(0f, match.GetHeadquarters(Side.Austria).CaptureProgress);
        }

        [Test]
        public void Domination_WinsAfterHoldingTwoThirdsForDominationTime()
        {
            Match match = CreateMatch();
            for (int i = 0; i < 5; i++)
            {
                Village village = match.AddVillage(new float2(-20f + i * 10f, 30f));
                village.Owner = i < 4 ? Side.Austria : Side.France;
            }

            match.AddSquad(Side.France, new float2(-20f, -20f), 0f, Formation.Line);
            match.AddSquad(Side.Austria, new float2(20f, -20f), 180f, Formation.Line);

            Assert.AreEqual(4, match.DominationVillagesRequired, "⅔ из 5 округляется вверх до 4");
            int ticks = TicksUntilResult(match, 400f);

            Assert.That(ticks, Is.InRange(TicksFor(180f) - 1, TicksFor(180f) + 1));
            Assert.AreEqual(Side.Austria, match.Result.Winner);
            Assert.AreEqual(VictoryReason.Domination, match.Result.Reason);
        }

        [Test]
        public void Domination_TimerResets_WhenVillageIsLost()
        {
            Match match = CreateMatch();
            var villages = Enumerable.Range(0, 5).Select(i => match.AddVillage(new float2(-20f + i * 10f, 30f))).ToArray();
            foreach (Village village in villages.Take(4))
            {
                village.Owner = Side.Austria;
            }

            match.AddSquad(Side.France, new float2(-20f, -20f), 0f, Formation.Line);
            match.AddSquad(Side.Austria, new float2(20f, -20f), 180f, Formation.Line);

            RunTicks(match, TicksFor(100f));
            Assert.AreEqual(100f, match.GetDominationTime(Side.Austria), 0.2f);

            villages[0].Owner = Side.France;
            match.Tick();
            Assert.AreEqual(0f, match.GetDominationTime(Side.Austria));

            villages[0].Owner = Side.Austria;
            RunTicks(match, TicksFor(100f));
            Assert.IsNull(match.Result, "Отсчёт начался заново");
        }

        [Test]
        public void Annihilation_SideWithoutSquadsAndBarracks_Loses()
        {
            var match = new Match(1, TestSettings.Create());
            match.EnableVictoryConditions();
            match.AddHeadquarters(Side.France, FranceHq);
            match.AddHeadquarters(Side.Austria, AustriaHq);
            match.AddSquad(Side.France, float2.zero, 0f, Formation.Line);

            match.Tick();

            Assert.IsNotNull(match.Result);
            Assert.AreEqual(Side.France, match.Result.Winner);
            Assert.AreEqual(VictoryReason.Annihilation, match.Result.Reason);
        }

        [Test]
        public void Annihilation_IsNotDeclared_WhileSideCanRecruit()
        {
            Match match = CreateMatch();
            match.AddSquad(Side.France, float2.zero, 0f, Formation.Line);

            RunTicks(match, 10);
            Assert.IsNull(match.Result, "Есть казарма и золото на отряд");

            match.GetPlayer(Side.Austria).Gold = 0;
            match.GetPlayer(Side.Austria).Reserve = 36;
            RunTicks(match, 10);
            Assert.IsNull(match.Result, "Полный резерв — можно нанять бесплатно");

            match.GetPlayer(Side.Austria).Reserve = 0;
            match.Buildings.First(b => b.Owner == Side.Austria && b.Type == BuildingType.Barracks).RecruitQueue = 1;
            RunTicks(match, 1);
            Assert.IsNull(match.Result, "Отряд уже в очереди найма");
        }

        [Test]
        public void Annihilation_WithBarracksButNoGoldOrReserve_Loses()
        {
            Match match = CreateMatch();
            match.AddSquad(Side.France, float2.zero, 0f, Formation.Line);
            match.GetPlayer(Side.Austria).Gold = 59;

            match.Tick();

            Assert.AreEqual(Side.France, match.Result?.Winner);
        }

        [Test]
        public void VictoryConditions_AreOffByDefault()
        {
            var match = new Match(1, TestSettings.Create());
            match.AddSquad(Side.France, float2.zero, 0f, Formation.Line);

            RunTicks(match, 50);

            Assert.IsNull(match.Result);
        }

        [Test]
        public void FinishedMatch_StopsChanging()
        {
            var match = new Match(1, TestSettings.Create());
            match.EnableVictoryConditions();
            match.AddHeadquarters(Side.France, FranceHq);
            match.AddHeadquarters(Side.Austria, AustriaHq);
            Squad squad = match.AddSquad(Side.France, float2.zero, 0f, Formation.Line);
            match.Tick();
            Assert.IsNotNull(match.Result);
            int tick = match.CurrentTick;

            match.Enqueue(new MoveCommand(squad.Id, new float2(20f, 0f), 0f));
            RunTicks(match, 20);

            Assert.AreEqual(tick, match.CurrentTick);
            Assert.AreEqual(float2.zero, squad.Position);
        }

        [Test]
        public void AiVsAi_FullMap_EndsWithin20000Ticks([Values(1, 2, 3)] int seed)
        {
            var match = new Match((ulong)seed, TestSettings.Create());
            match.EnableVictoryConditions();
            match.AddHeadquarters(Side.France, FranceHq);
            match.AddHeadquarters(Side.Austria, AustriaHq);
            match.AddVillage(new float2(0f, 0f));
            match.AddVillage(new float2(-20f, 24f));
            match.AddVillage(new float2(20f, 24f));
            match.AddVillage(new float2(-20f, -24f));
            match.AddVillage(new float2(20f, -24f));
            foreach (Side side in new[] { Side.France, Side.Austria })
            {
                float x = side == Side.France ? -44f : 44f;
                float facing = side == Side.France ? 0f : 180f;
                match.AddSquad(side, new float2(x, 7f), facing, Formation.Line);
                match.AddSquad(side, new float2(x, -7f), facing, Formation.Line);
                match.AddController(new TacticalAi(side));
                match.AddController(new StrategicAi(side));
            }

            for (int tick = 0; tick < 20000 && match.Result == null; tick++)
            {
                match.Tick();
            }

            Assert.IsNotNull(match.Result, $"seed {seed}: матч ИИ против ИИ не закончился за 20 000 тиков");
        }
    }
}
