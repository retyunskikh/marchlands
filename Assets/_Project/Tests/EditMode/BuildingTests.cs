using Marchlands.Simulation.Combat;
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
        public void Barracks_TakesBuildTime_AndCannotHireBeforeReady()
        {
            Match match = CreateMatch();
            match.Enqueue(new BuildBarracksCommand(Side.France, FranceHq + new float2(0f, 6f)));
            match.Tick();
            Building barracks = match.Buildings.Single(b => b.Type == BuildingType.Barracks);

            Assert.IsFalse(BuildRules.CanHire(barracks));
            int ticks = 1;
            while (!barracks.IsBuilt)
            {
                match.Tick();
                ticks++;
                Assert.Less(ticks, TicksFor(60f));
            }

            Assert.That(ticks, Is.InRange(TicksFor(30f) - 1, TicksFor(30f) + 1));
            Assert.IsTrue(BuildRules.CanHire(barracks));
        }

        [Test]
        public void Hiring_BringsOneRecruitPerInterval_ForGold()
        {
            Match match = CreateMatch();
            Building barracks = BuildReadyBarracks(match, Side.France, FranceHq + new float2(0f, 6f));
            PlayerState player = match.GetPlayer(Side.France);
            player.Gold = 100;
            int incomeBefore = player.Gold;

            match.Enqueue(new SetHiringCommand(barracks.Id, UnitType.Grenadiers, true));
            RunTicks(match, TicksFor(2f) - 1);
            Assert.AreEqual(0, player.GrenadierReserve, "Первый рекрут приходит через hireInterval");

            match.Tick();
            Assert.AreEqual(1, player.GrenadierReserve);

            RunTicks(match, TicksFor(10f));
            Assert.AreEqual(6, player.GrenadierReserve);
            Assert.AreEqual(0, player.FusilierReserve, "Найм фузилёров не включали");
            // 6 рекрутов по 2 золота; доход штаба за это время — не больше трёх выплат по 2.
            Assert.That(player.Gold, Is.InRange(incomeBefore - 12, incomeBefore - 12 + 6));
        }

        [Test]
        public void Hiring_BothInfantryTypes_Alternates()
        {
            Match match = CreateMatch();
            Building barracks = BuildReadyBarracks(match, Side.France, FranceHq + new float2(0f, 6f));
            PlayerState player = match.GetPlayer(Side.France);
            player.Gold = 100;

            match.Enqueue(new SetHiringCommand(barracks.Id, UnitType.Grenadiers, true));
            match.Enqueue(new SetHiringCommand(barracks.Id, UnitType.Fusiliers, true));
            RunTicks(match, TicksFor(12f));

            Assert.AreEqual(3, player.GrenadierReserve);
            Assert.AreEqual(3, player.FusilierReserve, "Рекруты приходят по очереди");
        }

        [Test]
        public void Hiring_Waits_WithoutGold_AndWhenEnoughPeopleWait()
        {
            Match match = CreateMatch();
            Building barracks = BuildReadyBarracks(match, Side.France, FranceHq + new float2(0f, 6f));
            PlayerState player = match.GetPlayer(Side.France);
            player.Gold = 3;

            match.Enqueue(new SetHiringCommand(barracks.Id, UnitType.Grenadiers, true));
            RunTicks(match, TicksFor(4f));
            Assert.AreEqual(1, player.GrenadierReserve, "На второго рекрута золота нет");
            Assert.AreEqual(1, player.Gold);
            Assert.AreEqual(HireWait.NoGold, BuildRules.IsWaiting(match, barracks));

            player.Gold = 100;
            player.GrenadierReserve = 72;
            RunTicks(match, TicksFor(4f));
            Assert.AreEqual(72, player.GrenadierReserve, "Ждут люди на два отряда — найм приостановлен");
            Assert.AreEqual(HireWait.Full, BuildRules.IsWaiting(match, barracks));
            Assert.GreaterOrEqual(player.Gold, 100, "Золото на найм не тратится (может прийти доход штаба)");
        }

        [Test]
        public void Hiring_FullType_IsSkipped_OtherKeepsHiring()
        {
            Match match = CreateMatch();
            Building barracks = BuildReadyBarracks(match, Side.France, FranceHq + new float2(0f, 6f));
            PlayerState player = match.GetPlayer(Side.France);
            player.Gold = 100;
            player.FusilierReserve = 32;

            match.Enqueue(new SetHiringCommand(barracks.Id, UnitType.Grenadiers, true));
            match.Enqueue(new SetHiringCommand(barracks.Id, UnitType.Fusiliers, true));
            RunTicks(match, TicksFor(8f));

            Assert.AreEqual(32, player.FusilierReserve);
            Assert.AreEqual(4, player.GrenadierReserve);
        }

        [Test]
        public void Hiring_TurnedOff_StopsRecruits()
        {
            Match match = CreateMatch();
            Building barracks = BuildReadyBarracks(match, Side.France, FranceHq + new float2(0f, 6f));
            match.GetPlayer(Side.France).Gold = 100;

            match.Enqueue(new SetHiringCommand(barracks.Id, UnitType.Fusiliers, true));
            RunTicks(match, TicksFor(2f));
            match.Enqueue(new SetHiringCommand(barracks.Id, UnitType.Fusiliers, false));
            RunTicks(match, TicksFor(10f));

            Assert.AreEqual(1, match.GetPlayer(Side.France).FusilierReserve);
            Assert.IsFalse(barracks.IsHiringAny);
        }

        [Test]
        public void Barracks_CannotHireCavalry()
        {
            Match match = CreateMatch();
            Building barracks = BuildReadyBarracks(match, Side.France, FranceHq + new float2(0f, 6f));

            match.Enqueue(new SetHiringCommand(barracks.Id, UnitType.Cavalry, true));
            match.Tick();

            Assert.IsFalse(barracks.IsHiringAny);
        }

        [Test]
        public void FormSquad_TakesPeopleFromReserve_AndSpawnsAtBarracks()
        {
            Match match = CreateMatch();
            Building barracks = BuildReadyBarracks(match, Side.France, FranceHq + new float2(0f, 6f));
            PlayerState player = match.GetPlayer(Side.France);
            player.FusilierReserve = 20;
            player.GrenadierReserve = 20;

            Assert.IsTrue(BuildRules.CanFormSquad(match, Side.France, UnitType.Fusiliers));
            Assert.IsFalse(BuildRules.CanFormSquad(match, Side.France, UnitType.Grenadiers), "На гренадёров людей мало");

            match.Enqueue(new FormSquadCommand(Side.France, UnitType.Fusiliers));
            match.Tick();

            Squad squad = match.Squads.Single(s => !s.IsCamp);
            Assert.AreEqual(UnitType.Fusiliers, squad.Type);
            Assert.AreEqual(16, squad.Alive);
            Assert.AreEqual(4, player.FusilierReserve);
            Assert.AreEqual(20, player.GrenadierReserve, "Резерв гренадёров отдельный");
            Assert.AreSame(squad, match.LastTickFormedSquads.Single(), "О новом отряде сообщается — его выделит ввод");
            // Встал в 4 u от казармы в сторону вражеского штаба (вверх), фронтом к нему.
            Assert.AreEqual(barracks.Position.y + 4f, squad.Position.y, 0.05f);
            Assert.AreEqual(0f, Angles.Delta(squad.Facing, 90f), 1e-3f);
        }

        [Test]
        public void FormSquad_WithoutEnoughPeople_OrBuiltBarracks_IsRefused()
        {
            Match match = CreateMatch();
            PlayerState player = match.GetPlayer(Side.France);
            player.GrenadierReserve = 100;
            match.Enqueue(new FormSquadCommand(Side.France, UnitType.Grenadiers));
            match.Tick();
            Assert.AreEqual(0, match.Squads.Count(s => !s.IsCamp), "Казармы нет");

            BuildReadyBarracks(match, Side.France, FranceHq + new float2(0f, 6f));
            player.GrenadierReserve = 35;
            match.Enqueue(new FormSquadCommand(Side.France, UnitType.Grenadiers));
            match.Tick();
            Assert.AreEqual(0, match.Squads.Count(s => !s.IsCamp), "Людей меньше, чем в отряде");
            Assert.AreEqual(35, player.GrenadierReserve);
        }

        [Test]
        public void WaitingPeople_StandAsCamp_InFrontOfBarracks()
        {
            Match match = CreateMatch();
            Building barracks = BuildReadyBarracks(match, Side.France, FranceHq + new float2(0f, 6f));
            PlayerState player = match.GetPlayer(Side.France);
            player.GrenadierReserve = 7;
            player.FusilierReserve = 5;

            match.Tick();

            Squad camp = match.Squads.Single(s => s.IsCamp);
            Assert.AreEqual(12, camp.Alive);
            Assert.IsFalse(camp.IsCavalryCamp);
            Assert.AreEqual(3f, math.distance(camp.Position, barracks.Position), 0.05f, "Стоят в campOffset перед казармой");

            player.GrenadierReserve = 0;
            player.FusilierReserve = 0;
            match.Tick();
            match.Tick();
            Assert.IsFalse(match.Squads.Any(s => s.IsCamp), "Резерв пуст — толпы нет");
        }

        [Test]
        public void CampLosses_AreTakenFromReserve_AndCampDoesNotRout()
        {
            Match match = CreateMatch();
            BuildReadyBarracks(match, Side.France, FranceHq + new float2(0f, 6f));
            PlayerState player = match.GetPlayer(Side.France);
            player.GrenadierReserve = 10;
            player.FusilierReserve = 10;
            match.Tick();
            Squad camp = match.Squads.Single(s => s.IsCamp);

            // Будто по толпе пришёлся залп: 6 убитых.
            camp.Alive -= 6;
            camp.Morale = 0f;
            match.Tick();

            Assert.AreEqual(14, player.GrenadierReserve + player.FusilierReserve);
            Assert.AreEqual(14, camp.Alive);
            Assert.IsFalse(camp.IsRouting, "Толпа рекрутов не бежит");
        }

        [Test]
        public void EnemyCanVolleyAtCamp()
        {
            Match match = CreateMatch();
            Building barracks = BuildReadyBarracks(match, Side.France, FranceHq + new float2(0f, 6f));
            match.GetPlayer(Side.France).GrenadierReserve = 20;
            match.Tick();
            Squad camp = match.Squads.Single(s => s.IsCamp);
            Squad enemy = match.AddSquad(Side.Austria, camp.Position + new float2(0f, 8f), -90f, Formation.Line);

            Assert.IsTrue(VolleyRules.CanVolley(match, enemy), "Толпа рекрутов — законная цель");
            Assert.IsFalse(VolleyRules.CanVolley(match, camp), "Рекруты не стреляют");
            Assert.IsNotNull(barracks);
        }

        [Test]
        public void Camp_ShootsSkirmishFire_AtNearbyEnemy()
        {
            Match match = CreateMatch();
            BuildReadyBarracks(match, Side.France, FranceHq + new float2(0f, 6f));
            match.GetPlayer(Side.France).GrenadierReserve = 30;
            match.Tick();
            Squad camp = match.Squads.Single(s => s.IsCamp);
            Squad enemy = match.AddSquad(Side.Austria, camp.Position + new float2(0f, 6f), -90f, Formation.Line);

            int shots = 0;
            for (int i = 0; i < TicksFor(10f); i++)
            {
                match.Tick();
                shots += match.LastTickVolleys.Count(v => v.ShooterId == camp.Id);
                Assert.IsTrue(match.LastTickVolleys.Where(v => v.ShooterId == camp.Id).All(v => v.Shooters == 1), "Каждый сам по себе, не залпом");
            }

            // 30 рекрутов, раз в 14 с каждый: за 10 с — около 21 выстрела.
            Assert.That(shots, Is.InRange(17, 23));
            Assert.Less(enemy.Alive, 36, "По врагу попадают");
        }

        [Test]
        public void Camp_AimsEachShot_BeforeFiring()
        {
            Match match = CreateMatch();
            BuildReadyBarracks(match, Side.France, FranceHq + new float2(0f, 6f));
            match.GetPlayer(Side.France).GrenadierReserve = 30;
            match.Tick();
            Squad camp = match.Squads.Single(s => s.IsCamp);
            match.AddSquad(Side.Austria, camp.Position + new float2(0f, 6f), -90f, Formation.Line);

            int firstAimTick = -1;
            int firstShotTick = -1;
            for (int i = 0; i < TicksFor(5f) && firstShotTick < 0; i++)
            {
                match.Tick();
                if (firstAimTick < 0 && camp.SkirmishAims.Count > 0)
                {
                    firstAimTick = i;
                }

                if (match.LastTickVolleys.Any(v => v.ShooterId == camp.Id))
                {
                    firstShotTick = i;
                    Assert.IsTrue(match.LastTickVolleys.Where(v => v.ShooterId == camp.Id).All(v => v.ShooterIndex >= 0),
                        "У выстрела вразнобой известен стрелок");
                }
            }

            Assert.GreaterOrEqual(firstAimTick, 0, "Рекрут вскидывает ружьё");
            Assert.AreEqual(TicksFor(match.Settings.combat.aimTime), firstShotTick - firstAimTick, 1, "Выстрел через aimTime после прицеливания");
        }

        [Test]
        public void Headquarters_CannotHire()
        {
            Match match = CreateMatch();

            Assert.IsFalse(BuildRules.CanHire(match.GetHeadquarters(Side.France)));
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

            RunTicks(match, 5);

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
            Assert.AreEqual(20, match.GetPlayer(Side.France).GrenadierReserve);
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

            // После постройки остаётся 30 золота, штаб даёт +2 за 5 с: на 16 фузилёров (32 золота) хватит к ~50-й секунде.
            RunTicks(match, TicksFor(100f));
            Assert.IsTrue(barracks.IsHiring(UnitType.Grenadiers) && barracks.IsHiring(UnitType.Fusiliers), "ИИ включает найм пехоты");
            Squad formed = match.Squads.FirstOrDefault(s => s.Side == Side.Austria && !s.IsCamp);
            Assert.IsNotNull(formed, "ИИ формирует отряд из рекрутов");
            Assert.AreEqual(UnitType.Fusiliers, formed.Type, "Первыми — фузилёры");
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
