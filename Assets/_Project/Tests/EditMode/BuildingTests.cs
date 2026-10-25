using System.Linq;
using Marchlands.Simulation.AI;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class BuildingTests
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

        private static Match CreateMatch()
        {
            var match = new Match(1, TestSettings.Create());
            match.AddHeadquarters(Side.France, FranceHq);
            match.AddHeadquarters(Side.Austria, AustriaHq);
            return match;
        }

        private static Building BuildReadyBarracks(Match match, Side side, float2 position)
        {
            match.Enqueue(new BuildBarracksCommand(side, position));
            match.Tick();
            Building barracks = match.Buildings.Last(b => b.Type == BuildingType.Barracks && b.Owner == side);
            barracks.BuildRemaining = 0f;
            return barracks;
        }

        [Test]
        public void BuildBarracks_InsideRadius_ChargesGold_AndStartsConstruction()
        {
            Match match = CreateMatch();

            match.Enqueue(new BuildBarracksCommand(Side.France, FranceHq + new float2(0f, 6f)));
            match.Tick();

            Building barracks = match.Buildings.Single(b => b.Type == BuildingType.Barracks);
            Assert.AreEqual(Side.France, barracks.Owner);
            Assert.IsFalse(barracks.IsBuilt);
            Assert.AreEqual(150 - 120, match.GetPlayer(Side.France).Gold);
        }

        [Test]
        public void BuildBarracks_OutsideRadius_IsRefused()
        {
            Match match = CreateMatch();

            Assert.IsFalse(BuildRules.CanBuildBarracks(match, Side.France, FranceHq + new float2(0f, 10.5f)));
            match.Enqueue(new BuildBarracksCommand(Side.France, FranceHq + new float2(0f, 10.5f)));
            match.Tick();

            Assert.IsFalse(match.Buildings.Any(b => b.Type == BuildingType.Barracks));
            Assert.AreEqual(150, match.GetPlayer(Side.France).Gold);
        }

        [Test]
        public void BuildBarracks_WithoutGold_IsRefused()
        {
            Match match = CreateMatch();
            match.GetPlayer(Side.France).Gold = 119;

            Assert.IsFalse(BuildRules.CanBuildBarracks(match, Side.France, FranceHq + new float2(0f, 6f)));
            match.Enqueue(new BuildBarracksCommand(Side.France, FranceHq + new float2(0f, 6f)));
            match.Tick();

            Assert.IsFalse(match.Buildings.Any(b => b.Type == BuildingType.Barracks));
            Assert.AreEqual(119, match.GetPlayer(Side.France).Gold);
        }

        [Test]
        public void BuildBarracks_TooCloseToBuildingOrVillage_IsRefused()
        {
            Match match = CreateMatch();
            match.AddVillage(FranceHq + new float2(6f, 0f));

            Assert.IsFalse(BuildRules.IsValidBarracksSite(match, Side.France, FranceHq + new float2(0f, 2f)), "Вплотную к штабу");
            Assert.IsFalse(BuildRules.IsValidBarracksSite(match, Side.France, FranceHq + new float2(6f, 2f)), "Вплотную к деревне");
            Assert.IsTrue(BuildRules.IsValidBarracksSite(match, Side.France, FranceHq + new float2(-6f, 0f)));
        }

        [Test]
        public void BuildBarracks_NearEnemyHq_IsRefused()
        {
            Match match = CreateMatch();

            Assert.IsFalse(BuildRules.IsValidBarracksSite(match, Side.France, AustriaHq + new float2(0f, -6f)));
        }

        [Test]
        public void Barracks_TakesBuildTime_AndCannotRecruitBeforeReady()
        {
            Match match = CreateMatch();
            match.Enqueue(new BuildBarracksCommand(Side.France, FranceHq + new float2(0f, 6f)));
            match.Tick();
            Building barracks = match.Buildings.Single(b => b.Type == BuildingType.Barracks);
            match.GetPlayer(Side.France).Gold = 1000;

            Assert.IsFalse(BuildRules.CanRecruit(match, barracks));
            int ticks = 1;
            while (!barracks.IsBuilt)
            {
                match.Tick();
                ticks++;
                Assert.Less(ticks, TicksFor(60f));
            }

            Assert.That(ticks, Is.InRange(TicksFor(30f) - 1, TicksFor(30f) + 1));
            Assert.IsTrue(BuildRules.CanRecruit(match, barracks));
        }

        [Test]
        public void Recruit_ChargesGold_AndSpawnsSquadAfterRecruitTime()
        {
            Match match = CreateMatch();
            Building barracks = BuildReadyBarracks(match, Side.France, FranceHq + new float2(0f, 6f));
            match.GetPlayer(Side.France).Gold = 100;

            match.Enqueue(new RecruitCommand(barracks.Id));
            match.Tick();
            Assert.AreEqual(40, match.GetPlayer(Side.France).Gold);
            Assert.AreEqual(0, match.Squads.Count);

            int ticks = 1;
            while (match.Squads.Count == 0)
            {
                match.Tick();
                ticks++;
                Assert.Less(ticks, TicksFor(60f));
            }

            Assert.That(ticks, Is.InRange(TicksFor(20f) - 1, TicksFor(20f) + 1));
            Squad squad = match.Squads[0];
            Assert.AreEqual(Side.France, squad.Side);
            Assert.AreEqual(36, squad.Alive);
            // Появился в 4 u от казармы в сторону вражеского штаба (вверх), фронтом к нему.
            Assert.AreEqual(barracks.Position.y + 4f, squad.Position.y, 0.05f);
            Assert.AreEqual(0f, Angles.Delta(squad.Facing, 90f), 1e-3f);
        }

        [Test]
        public void RecruitQueue_IsLimited_AndRecruitsOneByOne()
        {
            Match match = CreateMatch();
            Building barracks = BuildReadyBarracks(match, Side.France, FranceHq + new float2(0f, 6f));
            match.GetPlayer(Side.France).Gold = 1000;

            for (int i = 0; i < 4; i++)
            {
                match.Enqueue(new RecruitCommand(barracks.Id));
            }

            match.Tick();
            Assert.AreEqual(3, barracks.RecruitQueue, "Очередь не длиннее recruitQueueMax");
            Assert.AreEqual(1000 - 3 * 60, match.GetPlayer(Side.France).Gold);
            Assert.IsFalse(BuildRules.CanRecruit(match, barracks));

            RunTicks(match, TicksFor(20f));
            Assert.AreEqual(1, match.Squads.Count);
            RunTicks(match, TicksFor(20f));
            Assert.AreEqual(2, match.Squads.Count);
            RunTicks(match, TicksFor(20f));
            Assert.AreEqual(3, match.Squads.Count);
            Assert.AreEqual(0, barracks.RecruitQueue);
        }

        [Test]
        public void Recruit_WithoutGold_IsRefused()
        {
            Match match = CreateMatch();
            Building barracks = BuildReadyBarracks(match, Side.France, FranceHq + new float2(0f, 6f));
            match.GetPlayer(Side.France).Gold = 59;

            match.Enqueue(new RecruitCommand(barracks.Id));
            match.Tick();

            Assert.AreEqual(0, barracks.RecruitQueue);
            Assert.AreEqual(59, match.GetPlayer(Side.France).Gold);
        }

        [Test]
        public void Headquarters_CannotRecruit()
        {
            Match match = CreateMatch();
            match.GetPlayer(Side.France).Gold = 1000;

            Assert.IsFalse(BuildRules.CanRecruit(match, match.GetHeadquarters(Side.France)));
        }

        [Test]
        public void Headquarters_BringsIncome()
        {
            Match match = CreateMatch();

            RunTicks(match, TicksFor(5f));

            Assert.AreEqual(152, match.GetPlayer(Side.France).Gold);
        }

        [Test]
        public void RoutingSquad_RunsToOwnHeadquarters()
        {
            Match match = CreateMatch();
            match.SetRetreatPoint(Side.France, new float2(50f, 0f));
            Squad squad = match.AddSquad(Side.France, new float2(0f, 0f), 90f, Formation.Line);
            squad.Morale = 5f;

            RunTicks(match, 3);

            Assert.AreEqual(0f, Angles.Delta(squad.Facing, -90f), 1e-3f, "Бежит к штабу, а не к точке отхода сценария");
        }

        [Test]
        public void RoutingSquad_RunsToNearestBuiltBarracks_AndJoinsReserveThere()
        {
            Match match = CreateMatch();
            Building barracks = BuildReadyBarracks(match, Side.France, FranceHq + new float2(8f, 4f));
            Squad squad = match.AddSquad(Side.France, new float2(8f, 0f), 90f, Formation.Line, 20);
            squad.Morale = 5f;

            for (int i = 0; i < TicksFor(30f) && match.FindSquad(squad.Id) != null; i++)
            {
                match.Tick();
            }

            Assert.IsNull(match.FindSquad(squad.Id));
            Assert.AreEqual(20, match.GetPlayer(Side.France).Reserve);
            Assert.AreEqual(barracks.Position.y, FranceHq.y + 4f, 1e-4f);
        }

        [Test]
        public void RoutingSquad_SwitchesToBarracks_WhenItIsBuilt()
        {
            Match match = CreateMatch();
            match.Enqueue(new BuildBarracksCommand(Side.France, FranceHq + new float2(8f, 0f)));
            match.Tick();
            Building barracks = match.Buildings.Single(b => b.Type == BuildingType.Barracks);
            Squad squad = match.AddSquad(Side.France, new float2(20f, -10f), 90f, Formation.Line);
            squad.Morale = 5f;

            RunTicks(match, 3);
            float2 towardHq = math.normalize(FranceHq - squad.Position);
            Assert.AreEqual(0f, Angles.Delta(squad.Facing, Angles.Of(towardHq)), 1e-2f, "Пока казарма строится — к штабу");

            barracks.BuildRemaining = 0f;
            RunTicks(match, 2);
            float2 towardBarracks = math.normalize(barracks.Position - squad.Position);
            Assert.AreEqual(0f, Angles.Delta(squad.Facing, Angles.Of(towardBarracks)), 1e-2f, "Казарма построена — к ней");
        }

        [Test]
        public void Recruit_IsFreeFromReserve_OnlyWhenFullSquadGathered()
        {
            Match match = CreateMatch();
            Building barracks = BuildReadyBarracks(match, Side.France, FranceHq + new float2(0f, 6f));
            PlayerState player = match.GetPlayer(Side.France);
            player.Gold = 100;
            player.Reserve = 20;

            match.Enqueue(new RecruitCommand(barracks.Id));
            match.Tick();
            Assert.AreEqual(40, player.Gold, "Пока в резерве меньше отряда — найм за золото");
            Assert.AreEqual(20, player.Reserve);

            player.Reserve = 40;
            match.Enqueue(new RecruitCommand(barracks.Id));
            match.Tick();
            Assert.AreEqual(40, player.Gold, "Полный отряд в резерве — найм бесплатный");
            Assert.AreEqual(4, player.Reserve);
            Assert.AreEqual(2, barracks.RecruitQueue);
        }

        [Test]
        public void Recruit_FromReserve_WorksWithoutGold()
        {
            Match match = CreateMatch();
            Building barracks = BuildReadyBarracks(match, Side.France, FranceHq + new float2(0f, 6f));
            match.GetPlayer(Side.France).Gold = 0;
            match.GetPlayer(Side.France).Reserve = 36;

            Assert.IsTrue(BuildRules.CanRecruit(match, barracks));
        }

        [Test]
        public void StrategicAi_BuildsBarracksTowardEnemy_ThenRecruits()
        {
            Match match = CreateMatch();
            match.AddController(new StrategicAi(Side.Austria));

            RunTicks(match, TicksFor(2f));
            Building barracks = match.Buildings.SingleOrDefault(b => b.Type == BuildingType.Barracks);
            Assert.IsNotNull(barracks, "ИИ закладывает казарму в начале");
            Assert.AreEqual(Side.Austria, barracks.Owner);
            Assert.Less(barracks.Position.y, AustriaHq.y, "Казарма на стороне, обращённой к противнику");
            Assert.LessOrEqual(math.distance(barracks.Position, AustriaHq), 10f);

            // После постройки остаётся 30 золота, штаб даёт +2 за 5 с: на отряд (60) хватит примерно к 75-й секунде.
            RunTicks(match, TicksFor(100f));
            Assert.Greater(barracks.RecruitQueue + match.Squads.Count(s => s.Side == Side.Austria), 0, "ИИ нанимает отряды");
        }

        [Test]
        public void TacticalAi_SendsFreeSquadToDefendHeadquarters()
        {
            Match match = CreateMatch();
            // Свободный отряд ИИ далеко от штаба; к штабу подошёл враг.
            Squad defender = match.AddSquad(Side.Austria, new float2(30f, 0f), -90f, Formation.Line);
            match.AddSquad(Side.France, AustriaHq + new float2(0f, -15f), 90f, Formation.Line);
            match.AddController(new TacticalAi(Side.Austria));

            RunTicks(match, TicksFor(2f));

            Assert.AreEqual(SquadOrderType.Move, defender.Order.Type);
            Assert.Less(math.distance(defender.Order.Target, AustriaHq + new float2(0f, -15f)), 12f, "Идёт к врагу у штаба");
        }

        [Test]
        public void FullAiMatch_IsDeterministic()
        {
            string first = RunAiMatch(7);
            string second = RunAiMatch(7);

            Assert.AreEqual(first, second);
        }

        private static string RunAiMatch(ulong seed)
        {
            var match = new Match(seed, TestSettings.Create());
            match.AddHeadquarters(Side.France, FranceHq);
            match.AddHeadquarters(Side.Austria, AustriaHq);
            match.AddVillage(new float2(-15f, 0f));
            match.AddVillage(new float2(15f, 0f));
            foreach (Side side in new[] { Side.France, Side.Austria })
            {
                float y = side == Side.France ? -24f : 24f;
                match.AddSquad(side, new float2(-6f, y), side == Side.France ? 90f : -90f, Formation.Line);
                match.AddSquad(side, new float2(6f, y), side == Side.France ? 90f : -90f, Formation.Line);
                match.AddController(new TacticalAi(side));
                match.AddController(new StrategicAi(side));
            }

            RunTicks(match, TicksFor(180f));
            return string.Join(";", match.Squads.Select(s => $"{s.Id}:{s.Alive}:{s.Position.x:0.000}:{s.Position.y:0.000}"))
                + $"|{match.GetPlayer(Side.France).Gold}|{match.GetPlayer(Side.Austria).Gold}|{match.Buildings.Count}";
        }
    }
}
