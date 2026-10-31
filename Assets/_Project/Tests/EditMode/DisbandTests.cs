using System.Linq;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class DisbandTests
    {
        private static readonly float2 FranceHq = new float2(0f, -30f);

        private static Match CreateMatch(out Building barracks)
        {
            var match = new Match(1, TestSettings.Create());
            match.AddHeadquarters(Side.France, FranceHq);
            match.AddHeadquarters(Side.Austria, new float2(0f, 30f));
            barracks = match.AddBarracks(Side.France, FranceHq + new float2(0f, 6f));
            barracks.BuildRemaining = 0f;
            return match;
        }

        [Test]
        public void Disband_ClearsOnlyThatReserve_AndFreesPopulationAtOnce()
        {
            Match match = CreateMatch(out _);
            PlayerState player = match.GetPlayer(Side.France);
            player.FusilierReserve = 12;
            player.GrenadierReserve = 5;
            match.Tick();
            int populationBefore = BuildRules.GetPopulation(match, Side.France);
            int goldBefore = player.Gold;

            match.Enqueue(new DisbandReserveCommand(Side.France, UnitType.Fusiliers));
            match.Tick();

            Assert.AreEqual(0, player.FusilierReserve);
            Assert.AreEqual(5, player.GrenadierReserve, "Другую кучку не трогают");
            Assert.AreEqual(populationBefore - 12, BuildRules.GetPopulation(match, Side.France));
            Assert.LessOrEqual(player.Gold, goldBefore + match.Settings.economy.hqIncome, "Золото за распущенных не возвращается");
            Disband disband = match.LastTickDisbands.Single();
            Assert.AreEqual(Side.France, disband.Side);
            Assert.AreEqual(UnitType.Fusiliers, disband.Type);
            Assert.AreEqual(12, disband.Count);
        }

        [Test]
        public void Disband_ShrinksRecruitCamp_WithoutCountingLosses()
        {
            Match match = CreateMatch(out _);
            PlayerState player = match.GetPlayer(Side.France);
            player.FusilierReserve = 12;
            player.GrenadierReserve = 5;
            match.Tick();

            match.Enqueue(new DisbandReserveCommand(Side.France, UnitType.Grenadiers));
            match.Tick();
            match.Tick();

            Squad camp = match.Squads.Single(squad => squad.IsCamp);
            Assert.AreEqual(12, camp.Alive);
            Assert.AreEqual(12, player.FusilierReserve, "Распущенные не считаются погибшими");
        }

        [Test]
        public void Disband_StopsHiringThatType_Only()
        {
            Match match = CreateMatch(out Building barracks);
            match.GetPlayer(Side.France).GrenadierReserve = 3;
            match.Enqueue(new SetHiringCommand(barracks.Id, UnitType.Grenadiers, true));
            match.Enqueue(new SetHiringCommand(barracks.Id, UnitType.Fusiliers, true));
            match.Tick();

            match.Enqueue(new DisbandReserveCommand(Side.France, UnitType.Grenadiers));
            match.Tick();

            Assert.IsFalse(barracks.IsHiring(UnitType.Grenadiers), "Иначе резерв тут же начал бы набираться снова");
            Assert.IsTrue(barracks.IsHiring(UnitType.Fusiliers));
        }

        [Test]
        public void Disband_EmptyReserve_DoesNothing()
        {
            Match match = CreateMatch(out Building barracks);
            match.Enqueue(new SetHiringCommand(barracks.Id, UnitType.Grenadiers, true));
            match.Tick();

            match.Enqueue(new DisbandReserveCommand(Side.France, UnitType.Grenadiers));
            match.Tick();

            Assert.IsTrue(barracks.IsHiring(UnitType.Grenadiers));
            Assert.AreEqual(0, match.LastTickDisbands.Count);
        }
    }
}
