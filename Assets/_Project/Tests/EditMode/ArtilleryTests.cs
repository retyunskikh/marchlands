using System.Linq;
using Marchlands.Simulation.AI;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    /// <summary>Пушки: передок, ядро с рикошетом, картечь, артдепо и найм, ИИ.</summary>
    public class ArtilleryTests
    {
        private static readonly float2 FranceHq = new float2(0f, -30f);
        private static readonly float2 AustriaHq = new float2(0f, 30f);

        private static int TicksFor(float seconds)
        {
            return (int)math.round(seconds * SimulationTime.TicksPerSecond);
        }

        private static void Run(Match match, float seconds)
        {
            for (int i = 0; i < TicksFor(seconds); i++)
            {
                match.Tick();
            }
        }

        /// <summary>Пушка французов в начале координат стволом на север, уже снятая с передка.</summary>
        private static Squad ReadyGun(Match match)
        {
            Squad gun = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line, UnitType.Artillery);
            gun.IsLimbered = false;
            return gun;
        }

        /// <summary>Где сейчас сама пушка: в бою — впереди фронта расчёта, на передке — позади тыла.</summary>
        private static float2 GunPoint(Match match, Squad gun)
        {
            float half = FormationLayout.GetHalfExtents(gun, match.Settings.squads).y;
            float2 forward = Angles.Direction(gun.Facing);
            return gun.IsLimbered
                ? gun.Position - forward * (half + match.Settings.guns.towLength)
                : gun.Position + forward * (half + match.Settings.guns.gunAhead);
        }

        [Test]
        public void NewGun_UnlimbersAfterLimberTime_AndLimbersBeforeMoving()
        {
            var match = new Match(1, TestSettings.Create());
            Squad gun = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line, UnitType.Artillery);
            Assert.IsTrue(gun.IsLimbered, "Выезжает на передке");
            float2 towed = GunPoint(match, gun);
            Run(match, match.Settings.guns.limberTime + 0.2f);
            Assert.IsTrue(gun.IsGunReady, "Без приказа снимается с передка");
            Assert.Less(math.distance(GunPoint(match, gun), towed), 0.01f, "Пушка стоит на месте — расчёт обошёл её");
            Assert.Less(gun.Position.y, -1f, "Расчёт отошёл назад, за пушку");

            float2 firing = GunPoint(match, gun);
            match.Enqueue(new MoveCommand(gun.Id, new float2(0f, 10f), 90f));
            Run(match, match.Settings.guns.limberTime + 0.2f);
            Assert.IsTrue(gun.IsLimbered);
            Assert.Less(math.distance(GunPoint(match, gun), firing), 0.3f, "Беря на передок, пушку не двигают — расчёт выходит вперёд");
            Run(match, 2f);
            Assert.Greater(GunPoint(match, gun).y, firing.y + 0.1f, "На передке поехала");
        }

        [Test]
        public void TargetedBuilding_IsShelled_EvenWithEnemySquadNearer()
        {
            var match = new Match(1, TestSettings.Create());
            Squad gun = ReadyGun(match);
            Building blockhouse = match.AddBlockhouse(Side.Austria, new float2(0f, 25f));
            match.AddSquad(Side.Austria, new float2(0f, 14f), -90f, Formation.Line);
            match.Enqueue(TargetCommand.AtBuilding(gun.Id, blockhouse.Id));
            match.Tick();

            Assert.IsTrue(GunTargeting.TryGetTarget(match, gun, out Squad squad, out Building building));
            Assert.AreEqual(blockhouse, building, "Указанное здание важнее ближайшего отряда");
            Assert.IsNull(squad);

            float health = blockhouse.Health;
            Run(match, match.Settings.guns.aimTime + 1f);
            Assert.Less(blockhouse.Health, health, "Ядро попало по зданию");
        }

        [Test]
        public void ReadyGun_FiresRoundShotByItself()
        {
            var match = new Match(1, TestSettings.Create());
            Squad gun = ReadyGun(match);
            match.AddSquad(Side.Austria, new float2(0f, 20f), -90f, Formation.Column);
            bool fired = false;
            for (int i = 0; i < TicksFor(3f) && !fired; i++)
            {
                match.Tick();
                fired = match.LastTickArtilleryShots.Any(s => s.ShooterId == gun.Id && s.Mode == ShotMode.RoundShot);
            }

            Assert.IsTrue(fired, "Сама навелась и выстрелила");
            Assert.AreEqual(FireState.Reloading, gun.FireState);
        }

        [Test]
        public void RoundShot_DoesNotFireCloserThanMinRange()
        {
            var match = new Match(1, TestSettings.Create());
            ReadyGun(match);
            match.AddSquad(Side.Austria, new float2(0f, 4f), -90f, Formation.Line);
            Run(match, 3f);
            Assert.AreEqual(0, match.LastTickArtilleryShots.Count);
            Assert.AreEqual(FireState.Loaded, match.Squads[0].FireState, "Ядром так близко не бьёт (навесом)");
        }

        [Test]
        public void Ricochet_HitsDeepFormationAndFlankHarder()
        {
            var settings = TestSettings.Create().squads;
            var match = new Match(1, TestSettings.Create());
            Squad line = match.AddSquad(Side.Austria, float2.zero, -90f, Formation.Line, 30);
            Squad column = match.AddSquad(Side.Austria, new float2(50f, 0f), -90f, Formation.Column, 30);

            // Ядро летит с севера на юг — через строй фронтально.
            int lineFront = ArtillerySystem.MenOnPath(line, new float2(0f, 5f), new float2(0f, -5f), settings);
            int columnFront = ArtillerySystem.MenOnPath(column, new float2(50f, 5f), new float2(50f, -5f), settings);
            // Вдоль строя линии — с фланга.
            int lineFlank = ArtillerySystem.MenOnPath(line, new float2(-10f, 0f), new float2(10f, 0f), settings);

            Assert.AreEqual(2, lineFront, "Линию в две шеренги ядро пробивает на двоих");
            Assert.Greater(columnFront, lineFront, "Колонну — на всю глубину");
            Assert.Greater(lineFlank, columnFront, "Вдоль линии — страшнее всего");
            Assert.AreEqual(0, ArtillerySystem.MenOnPath(line, new float2(0f, 5f), new float2(0f, 3f), settings), "Не долетело — никого");
        }

        [Test]
        public void RoundShot_ThroughRanks_TakesSeveralPerRank_FlankUnchanged()
        {
            var settings = TestSettings.Create().squads;
            var match = new Match(1, TestSettings.Create());
            Squad line = match.AddSquad(Side.Austria, float2.zero, -90f, Formation.Line, 30);

            int front = ArtillerySystem.MenOnPath(line, new float2(0f, 5f), new float2(0f, -5f), settings, 3f);
            int flank = ArtillerySystem.MenOnPath(line, new float2(-10f, 0f), new float2(10f, 0f), settings, 3f);

            Assert.AreEqual(6, front, "по три человека в каждой из двух шеренг");
            Assert.AreEqual(ArtillerySystem.MenOnPath(line, new float2(-10f, 0f), new float2(10f, 0f), settings), flank,
                "вдоль шеренги — как прежде, по человеку на ряд");
        }

        [Test]
        public void RoundShot_MoraleShock_EvenWithoutCasualties()
        {
            var settings = TestSettings.Create();
            settings.guns.roundShotKillChance = 0f;
            settings.guns.roundShotMoraleShock = 8f;
            var match = new Match(1, settings);
            Squad gun = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line, UnitType.Artillery);
            gun.IsLimbered = false;
            Squad target = match.AddSquad(Side.Austria, new float2(0f, 20f), 270f, Formation.Column, 30);
            float before = target.Morale;
            bool shot = false;
            for (int i = 0; i < 400 && !shot; i++)
            {
                match.Tick();
                shot = match.LastTickArtilleryShots.Count > 0;
            }

            Assert.IsTrue(shot);
            Assert.AreEqual(30, target.Alive, "никого не убило");
            Assert.Less(target.Morale, before - 7f, "но дух упал от ядра сквозь строй");
        }

        [Test]
        public void Gun_IgnoresTargetOutsideSector()
        {
            var match = new Match(1, TestSettings.Create());
            Squad gun = ReadyGun(match);
            match.AddSquad(Side.Austria, new float2(20f, 2f), 180f, Formation.Line);
            Run(match, 3f);
            Assert.AreEqual(FireState.Loaded, gun.FireState, "Сбоку — вне сектора ствола");
        }

        [Test]
        public void Gun_CannotCharge_OrChangeFormation()
        {
            var match = new Match(1, TestSettings.Create());
            Squad gun = ReadyGun(match);
            Squad enemy = match.AddSquad(Side.Austria, new float2(0f, 10f), -90f, Formation.Line);
            match.Enqueue(new ChargeCommand(gun.Id, enemy.Id));
            match.Enqueue(new ChangeFormationCommand(gun.Id, Formation.Column));
            match.Tick();
            Assert.AreNotEqual(SquadOrderType.Charge, gun.Order.Type);
            Assert.AreEqual(Formation.Line, gun.TargetFormation);
        }

        private static Match CreateBaseMatch()
        {
            var match = new Match(1, TestSettings.Create());
            match.AddHeadquarters(Side.France, FranceHq);
            match.AddHeadquarters(Side.Austria, AustriaHq);
            return match;
        }

        [Test]
        public void Depot_HiresGunners_AndFormsGun()
        {
            Match match = CreateBaseMatch();
            match.GetPlayer(Side.France).Gold = 1000;
            match.Enqueue(new BuildDepotCommand(Side.France, FranceHq + new float2(0f, 8f)));
            match.Tick();
            Building depot = match.Buildings.Single(b => b.Type == BuildingType.ArtilleryDepot);
            Assert.AreEqual(1000 - match.Settings.buildings.depotCost, match.GetPlayer(Side.France).Gold);
            depot.BuildRemaining = 0f;

            // Одно нажатие — одна пушка: платят сразу, пушка готова через gunCrewHireInterval.
            int gold = match.GetPlayer(Side.France).Gold;
            match.Enqueue(new OrderGunCommand(depot.Id));
            match.Tick();
            Assert.IsTrue(depot.IsOrderingGun);
            Assert.GreaterOrEqual(gold - match.GetPlayer(Side.France).Gold, match.Settings.buildings.gunCrewHireCost - 5, "Заплатили сразу за всю пушку");
            // Пока первая готовится, вторую можно поставить в очередь (дороже), третью — уже нет.
            gold = match.GetPlayer(Side.France).Gold;
            match.Enqueue(new OrderGunCommand(depot.Id));
            match.Tick();
            Assert.AreEqual(1, depot.GunOrdersQueued, "Вторая — в очереди");
            Assert.Greater(gold - match.GetPlayer(Side.France).Gold, match.Settings.buildings.gunCrewHireCost, "Вторая дороже");
            gold = match.GetPlayer(Side.France).Gold;
            match.Enqueue(new OrderGunCommand(depot.Id));
            match.Tick();
            Assert.AreEqual(1, depot.GunOrdersQueued, "Очередь — одна пушка");
            Assert.Less(gold - match.GetPlayer(Side.France).Gold, 5, "Сверх очереди не берут денег");

            Run(match, match.Settings.buildings.gunCrewHireInterval);
            Assert.IsTrue(match.Squads.Any(s => s.IsArtillery), $"Пушка готова: осталось {depot.GunOrderRemaining}, отрядов {match.Squads.Count}, депо цело {!depot.IsDestroyed}");
            Squad gun = match.Squads.Single(s => s.IsArtillery);
            Assert.AreEqual(match.Settings.artillery.squadSize, gun.Alive, "Сразу со всем расчётом");
            Assert.IsTrue(depot.IsOrderingGun, "Сразу за первой готовят вторую из очереди");
            Assert.AreEqual(0, depot.GunOrdersQueued);
            Assert.Less(math.distance(gun.Position, depot.Position), 12f, "Встала у депо");
            Run(match, match.Settings.buildings.gunCrewHireInterval + 1f);
            Assert.AreEqual(2, match.Squads.Count(s => s.IsArtillery), "Вторая из очереди готова");
            Assert.IsFalse(depot.IsOrderingGun);
            Run(match, 5f);
            Assert.AreEqual(2, match.Squads.Count(s => s.IsArtillery), "Без нового нажатия пушек больше не появляется");
        }

        [Test]
        public void EachNextGun_CostsMore_WhilePreviousAlive()
        {
            Match match = CreateBaseMatch();
            Marchlands.Simulation.Config.BuildingSettings settings = match.Settings.buildings;
            Assert.AreEqual(settings.gunCrewHireCost, BuildRules.GetGunCost(match, Side.France));

            Squad first = match.AddSquad(Side.France, FranceHq + new float2(10f, 10f), 90f, Formation.Line, UnitType.Artillery);
            int second = BuildRules.GetGunCost(match, Side.France);
            Assert.AreEqual((int)math.round(settings.gunCrewHireCost * settings.gunCostGrowth), second, "Вторая дороже");

            match.AddSquad(Side.France, FranceHq + new float2(-10f, 10f), 90f, Formation.Line, UnitType.Artillery);
            Assert.Greater(BuildRules.GetGunCost(match, Side.France), second, "Третья ещё дороже");

            first.Alive = 0;
            Assert.AreEqual(second, BuildRules.GetGunCost(match, Side.France), "Погибла пушка — следующая снова дешевле");
        }

        [Test]
        public void BrokenCrew_RunsToHeadquarters_AndVanishes_WithoutReserve()
        {
            Match match = CreateBaseMatch();
            Squad gun = match.AddSquad(Side.France, FranceHq + new float2(12f, 10f), 90f, Formation.Line, UnitType.Artillery);
            // Осталась половина расчёта — меньше минимального рабочего, пушку бросают.
            gun.Alive = match.Settings.artillery.minSquadSize - 1;
            int reserveBefore = match.GetPlayer(Side.France).ArtilleryReserve;
            match.Tick();
            Assert.IsTrue(gun.IsRouting, "Неполный расчёт бросает пушку");

            Run(match, 60f);
            Assert.IsNull(match.FindSquad(gun.Id), "Добежали до штаба и исчезли");
            Assert.AreEqual(reserveBefore, match.GetPlayer(Side.France).ArtilleryReserve, "В резерв не вернулись");
        }

        [Test]
        public void TacticalAi_GunApproaches_AndFires()
        {
            var match = new Match(1, TestSettings.Create());
            Squad gun = match.AddSquad(Side.Austria, new float2(0f, 60f), -90f, Formation.Line, UnitType.Artillery);
            match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            match.AddController(new TacticalAi(Side.Austria));
            bool fired = false;
            for (int i = 0; i < TicksFor(120f) && !fired; i++)
            {
                match.Tick();
                fired = match.LastTickArtilleryShots.Any(s => s.ShooterId == gun.Id);
            }

            Assert.IsTrue(fired, "Подъехала на дистанцию ядра, снялась с передка и выстрелила");
            Assert.Greater(math.distance(gun.Position, float2.zero), match.Settings.guns.roundShotMinRange, "Не лезет вплотную");
        }

        [Test]
        public void StrategicAi_BuildsDepot_AndFieldsGun()
        {
            var settings = TestSettings.Create();
            settings.ai.artilleryGuns = 1;
            settings.ai.cavalrySquads = 0;
            var match = new Match(1, settings);
            match.AddHeadquarters(Side.France, FranceHq);
            match.AddHeadquarters(Side.Austria, AustriaHq);
            match.GetPlayer(Side.Austria).Gold = 5000;
            match.AddController(new StrategicAi(Side.Austria));
            Run(match, 240f);

            Assert.IsTrue(match.Buildings.Any(b => b.Owner == Side.Austria && b.Type == BuildingType.ArtilleryDepot), "Построил артдепо");
            Assert.IsTrue(match.Squads.Any(s => s.Side == Side.Austria && s.IsArtillery), "Выставил пушку");
        }
    }
}
