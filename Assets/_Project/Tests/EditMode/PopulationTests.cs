using System.Linq;
using Marchlands.Simulation.AI;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class PopulationTests
    {
        private static readonly float2 FranceHq = new float2(0f, -30f);
        private static readonly float2 AustriaHq = new float2(0f, 30f);

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

        private static Match CreateMatch(int headquartersPopulation)
        {
            SimulationSettings settings = TestSettings.Create();
            settings.buildings.headquartersPopulation = headquartersPopulation;
            var match = new Match(1, settings);
            match.AddHeadquarters(Side.France, FranceHq);
            match.AddHeadquarters(Side.Austria, AustriaHq);
            match.GetPlayer(Side.France).Gold = 2000;
            return match;
        }

        private static Building BuildReadyBarracks(Match match)
        {
            match.Enqueue(new BuildBarracksCommand(Side.France, FranceHq + new float2(0f, 6f)));
            match.Tick();
            Building barracks = match.Buildings.Last(b => b.Type == BuildingType.Barracks && b.Owner == Side.France);
            barracks.BuildRemaining = 0f;
            return barracks;
        }

        [Test]
        public void Population_CountsSoldiersAndReserves()
        {
            Match match = CreateMatch(60);
            match.AddSquad(Side.France, new float2(-10f, 0f), 90f, Formation.Line, 20);
            match.AddSquad(Side.Austria, new float2(10f, 0f), 90f, Formation.Line, 30);
            match.GetPlayer(Side.France).FusilierReserve = 7;

            Assert.AreEqual(27, BuildRules.GetPopulation(match, Side.France));
            Assert.AreEqual(60, BuildRules.GetPopulationCapacity(match, Side.France));
        }

        [Test]
        public void Hiring_Waits_WhenNoPlacesLeft()
        {
            Match match = CreateMatch(40);
            match.AddSquad(Side.France, new float2(-10f, 0f), 90f, Formation.Line, 36);
            Building barracks = BuildReadyBarracks(match);
            match.Enqueue(new SetHiringCommand(barracks.Id, UnitType.Fusiliers, true));

            RunTicks(match, TicksFor(30f));

            Assert.AreEqual(4, match.GetPlayer(Side.France).FusilierReserve, "Наняли ровно до предела");
            Assert.AreEqual(HireWait.NoHousing, BuildRules.IsWaiting(match, barracks));
        }

        [Test]
        public void House_AddsPlaces_AfterBuildTime_AndEachNextCostsDouble()
        {
            Match match = CreateMatch(60);
            Building headquarters = match.GetHeadquarters(Side.France);
            PlayerState player = match.GetPlayer(Side.France);
            int gold = player.Gold;
            Assert.AreEqual(80, BuildRules.GetHouseCost(match, Side.France));

            match.Enqueue(new BuildHouseCommand(Side.France));
            match.Tick();
            Assert.AreEqual(gold - 80, player.Gold);
            Assert.IsTrue(headquarters.IsBuildingHouse);
            Assert.IsFalse(BuildRules.CanBuildHouse(match, Side.France), "Строится по одному дому");
            Assert.AreEqual(60, BuildRules.GetPopulationCapacity(match, Side.France), "Пока строится — мест не прибавилось");

            RunTicks(match, TicksFor(15f));

            Assert.AreEqual(1, headquarters.Houses);
            Assert.AreEqual(90, BuildRules.GetPopulationCapacity(match, Side.France));
            Assert.AreEqual(160, BuildRules.GetHouseCost(match, Side.France), "Следующий дом вдвое дороже");
        }

        [Test]
        public void Houses_AreLimited()
        {
            Match match = CreateMatch(60);
            match.Settings.buildings.houseBuildTime = 0f;
            match.GetPlayer(Side.France).Gold = 100000;
            for (int i = 0; i < 6; i++)
            {
                match.Enqueue(new BuildHouseCommand(Side.France));
                match.Tick();
            }

            Assert.AreEqual(4, match.GetHeadquarters(Side.France).Houses);
            Assert.AreEqual(180, BuildRules.GetPopulationCapacity(match, Side.France));
            Assert.AreEqual(100000 - (80 + 160 + 320 + 640), match.GetPlayer(Side.France).Gold);
        }

        [Test]
        public void Reinforcement_Stops_WhenNoPlacesLeft()
        {
            Match match = CreateMatch(30);
            Village village = match.AddVillage(new float2(-10f, 0f));
            village.Owner = Side.France;
            Squad squad = match.AddSquad(Side.France, village.Position, 90f, Formation.Line, 30);
            squad.Alive = 28;
            match.GetPlayer(Side.France).FusilierReserve = 2;

            RunTicks(match, TicksFor(20f));

            Assert.AreEqual(28, squad.Alive, "Мест нет — деревня не пополняет");
        }

        [Test]
        public void Ai_BuildsHouse_WhenPlacesRunOut()
        {
            Match match = CreateMatch(60);
            match.AddSquad(Side.France, new float2(-10f, 0f), 90f, Formation.Line, 36);
            BuildReadyBarracks(match);
            match.GetPlayer(Side.France).FusilierReserve = 16;
            match.AddController(new StrategicAi(Side.France));

            RunTicks(match, TicksFor(5f));

            Building headquarters = match.GetHeadquarters(Side.France);
            Assert.IsTrue(headquarters.IsBuildingHouse || headquarters.Houses > 0, "Мест меньше houseMargin — ИИ строит дом");
        }
    }
}
