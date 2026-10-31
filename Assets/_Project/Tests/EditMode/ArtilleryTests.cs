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

        [Test]
        public void NewGun_UnlimbersAfterLimberTime_AndLimbersBeforeMoving()
        {
            var match = new Match(1, TestSettings.Create());
            Squad gun = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line, UnitType.Artillery);
            Assert.IsTrue(gun.IsLimbered, "Выезжает на передке");
            Run(match, match.Settings.guns.limberTime + 0.2f);
            Assert.IsTrue(gun.IsGunReady, "Без приказа снимается с передка");

            match.Enqueue(new MoveCommand(gun.Id, new float2(0f, 10f), 90f));
            Run(match, match.Settings.guns.limberTime - 0.5f);
            Assert.Less(math.length(gun.Position), 1e-4f, "Пока берут на передок — стоит");
            Run(match, 2f);
            Assert.IsTrue(gun.IsLimbered);
            Assert.Greater(gun.Position.y, 0.1f, "На передке поехала");
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
        public void Canister_KillsCloseTarget_AndRoundShotIsNotUsed()
        {
            int kills = 0;
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var match = new Match(seed, TestSettings.Create());
                Squad gun = ReadyGun(match);
                match.Enqueue(new SetShotModeCommand(gun.Id, ShotMode.Canister));
                match.AddSquad(Side.Austria, new float2(0f, 6f), -90f, Formation.Line, 30);
                for (int i = 0; i < TicksFor(3f); i++)
                {
                    match.Tick();
                    foreach (ArtilleryShot shot in match.LastTickArtilleryShots)
                    {
                        Assert.AreEqual(ShotMode.Canister, shot.Mode);
                        kills += shot.Casualties;
                    }
                }
            }

            // ~36 пуль × ~0.2 на 5 u ≈ 7 убитых за выстрел.
            Assert.Greater(kills / 20f, 3f, "Картечь вблизи страшна");
        }

        [Test]
        public void Canister_DoesNotReachFarTarget()
        {
            var match = new Match(1, TestSettings.Create());
            Squad gun = ReadyGun(match);
            match.Enqueue(new SetShotModeCommand(gun.Id, ShotMode.Canister));
            match.AddSquad(Side.Austria, new float2(0f, 20f), -90f, Formation.Line);
            Run(match, 3f);
            Assert.AreEqual(FireState.Loaded, gun.FireState, "Картечью так далеко не бьют");
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

            match.Enqueue(new SetHiringCommand(depot.Id, UnitType.Artillery, true));
            Run(match, match.Settings.buildings.gunnerHireInterval * match.Settings.artillery.squadSize + 1f);
            Assert.GreaterOrEqual(match.GetPlayer(Side.France).ArtilleryReserve, match.Settings.artillery.squadSize);
            Assert.IsTrue(BuildRules.CanFormSquad(match, Side.France, UnitType.Artillery));

            match.Enqueue(new FormSquadCommand(Side.France, UnitType.Artillery));
            match.Tick();
            Squad gun = match.Squads.Single(s => s.IsArtillery);
            Assert.AreEqual(match.Settings.artillery.squadSize, gun.Alive);
            Assert.Less(math.distance(gun.Position, depot.Position), 10f, "Встала у депо");
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
