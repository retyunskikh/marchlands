using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class ReinforcementTests
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

        private static Match CreateMatchWithOwnVillage(out Village village)
        {
            var match = new Match(1, TestSettings.Create());
            village = match.AddVillage(float2.zero);
            village.Owner = Side.France;
            return match;
        }

        [Test]
        public void SquadInOwnVillage_GainsOneManPerInterval_UpToFullSize()
        {
            Match match = CreateMatchWithOwnVillage(out Village village);
            Squad squad = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line, 33);

            RunTicks(match, TicksFor(6f) - 1);
            Assert.AreEqual(33, squad.Alive);
            match.Tick();
            Assert.AreEqual(34, squad.Alive);
            Assert.AreEqual(1, match.LastTickReinforcements.Count);
            Assert.AreEqual(squad.Id, match.LastTickReinforcements[0].SquadId);
            Assert.AreEqual(village.Id, match.LastTickReinforcements[0].VillageId);

            RunTicks(match, TicksFor(60f));
            Assert.AreEqual(36, squad.Alive, "Не больше полной численности");
        }

        [Test]
        public void ReinforcingFlag_AndProgress_ShowStateInVillage()
        {
            Match match = CreateMatchWithOwnVillage(out _);
            Squad inside = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line, 35);
            Squad outside = match.AddSquad(Side.France, new float2(0f, 20f), 90f, Formation.Line, 30);

            RunTicks(match, TicksFor(3f));
            Assert.IsTrue(inside.IsReinforcing);
            Assert.That(inside.ReinforceProgress, Is.EqualTo(0.5f).Within(0.02f));
            Assert.IsFalse(outside.IsReinforcing);
            Assert.AreEqual(0f, outside.ReinforceProgress);

            RunTicks(match, TicksFor(3f));
            Assert.AreEqual(36, inside.Alive);
            Assert.IsFalse(inside.IsReinforcing, "Полный отряд больше не пополняется");
        }

        [Test]
        public void Reinforcement_WorksEvenInBattle()
        {
            Match match = CreateMatchWithOwnVillage(out _);
            Squad squad = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line, 30);
            match.AddSquad(Side.Austria, new float2(0f, 8f), -90f, Formation.Line);

            RunTicks(match, TicksFor(6f));

            Assert.AreEqual(31, squad.Alive);
        }

        [Test]
        public void NoReinforcement_OutsideOwnVillage_ForRoutingOrCavalry()
        {
            Match match = CreateMatchWithOwnVillage(out _);
            Village enemyVillage = match.AddVillage(new float2(40f, 0f));
            enemyVillage.Owner = Side.Austria;
            Squad outside = match.AddSquad(Side.France, new float2(0f, 20f), 90f, Formation.Line, 30);
            Squad inEnemyVillage = match.AddSquad(Side.France, new float2(40f, 0f), 90f, Formation.Line, 30);
            Squad cavalry = match.AddSquad(Side.France, new float2(1f, 0f), 90f, Formation.Line, UnitType.Cavalry, 20);

            RunTicks(match, TicksFor(12f));

            Assert.AreEqual(30, outside.Alive);
            Assert.AreEqual(30, inEnemyVillage.Alive);
            Assert.AreEqual(20, cavalry.Alive, "Конница из деревень не пополняется");
        }

        [Test]
        public void LeavingVillage_ResetsTimer()
        {
            Match match = CreateMatchWithOwnVillage(out _);
            Squad squad = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line, 30);

            RunTicks(match, TicksFor(5f));
            squad.Position = new float2(0f, 20f);
            match.Tick();
            squad.Position = float2.zero;
            RunTicks(match, TicksFor(5f));

            Assert.AreEqual(30, squad.Alive, "Отсчёт начался заново");
        }
    }
}
