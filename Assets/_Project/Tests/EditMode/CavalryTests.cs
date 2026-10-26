using System.Linq;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class CavalryTests
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

        private static Building BuildReady(Match match, BuildingType type, float2 position)
        {
            match.GetPlayer(Side.France).Gold = 1000;
            match.Enqueue(type == BuildingType.Stable
                ? new BuildStableCommand(Side.France, position)
                : (ICommand)new BuildBarracksCommand(Side.France, position));
            match.Tick();
            Building building = match.Buildings.Last(b => b.Type == type);
            building.BuildRemaining = 0f;
            return building;
        }

        [Test]
        public void Stable_CostsGold_AndTakesBuildTime()
        {
            Match match = CreateMatch();
            match.GetPlayer(Side.France).Gold = 250;

            match.Enqueue(new BuildStableCommand(Side.France, FranceHq + new float2(0f, 6f)));
            match.Tick();

            Building stable = match.Buildings.Single(b => b.Type == BuildingType.Stable);
            Assert.AreEqual(50, match.GetPlayer(Side.France).Gold);
            Assert.IsFalse(stable.IsBuilt);
            RunTicks(match, TicksFor(40f));
            Assert.IsTrue(stable.IsBuilt);
        }

        [Test]
        public void Stable_HiresIntoCavalryReserve_UpToTwoSquads()
        {
            Match match = CreateMatch();
            Building stable = BuildReady(match, BuildingType.Stable, FranceHq + new float2(0f, 6f));
            PlayerState player = match.GetPlayer(Side.France);
            player.Gold = 1000;

            match.Enqueue(new SetHiringCommand(stable.Id, UnitType.Cavalry, true));
            RunTicks(match, TicksFor(3f));
            Assert.AreEqual(1, player.CavalryReserve);
            Assert.AreEqual(0, player.GrenadierReserve + player.FusilierReserve, "Пехотные резервы не трогаются");

            player.CavalryReserve = 48;
            int gold = player.Gold;
            RunTicks(match, TicksFor(9f));
            Assert.AreEqual(48, player.CavalryReserve, "Ждут всадники на два отряда — найм ждёт");
            Assert.GreaterOrEqual(player.Gold, gold);
            Assert.AreEqual(HireWait.Full, BuildRules.IsWaiting(match, stable));
        }

        [Test]
        public void Stable_CannotHireInfantry()
        {
            Match match = CreateMatch();
            Building stable = BuildReady(match, BuildingType.Stable, FranceHq + new float2(0f, 6f));

            match.Enqueue(new SetHiringCommand(stable.Id, UnitType.Grenadiers, true));
            match.Tick();

            Assert.IsFalse(stable.IsHiringAny);
        }

        [Test]
        public void FormCavalry_UsesCavalryReserve_AtStable()
        {
            Match match = CreateMatch();
            BuildReady(match, BuildingType.Barracks, FranceHq + new float2(-6f, 4f));
            Building stable = BuildReady(match, BuildingType.Stable, FranceHq + new float2(6f, 4f));
            PlayerState player = match.GetPlayer(Side.France);
            player.GrenadierReserve = 100;
            Assert.IsFalse(BuildRules.CanFormSquad(match, Side.France, UnitType.Cavalry), "Пехотный резерв на конницу не идёт");

            player.CavalryReserve = 30;
            match.Enqueue(new FormSquadCommand(Side.France, UnitType.Cavalry));
            match.Tick();

            Squad cavalry = match.Squads.Single(s => !s.IsCamp && s.Type == UnitType.Cavalry);
            Assert.AreEqual(UnitType.Cavalry, cavalry.Type);
            Assert.AreEqual(24, cavalry.Alive);
            Assert.AreEqual(6, player.CavalryReserve);
            Assert.AreEqual(100, player.GrenadierReserve);
            Assert.AreEqual(4f, math.distance(stable.Position, cavalry.Position), 0.05f, "Встаёт в spawnOffset от конюшни");
        }

        [Test]
        public void RoutedCavalry_RunsToStable_AndJoinsCavalryReserve()
        {
            Match match = CreateMatch();
            Building stable = BuildReady(match, BuildingType.Stable, FranceHq + new float2(8f, 4f));
            Squad cavalry = match.AddSquad(Side.France, new float2(8f, -10f), 90f, Formation.Line, UnitType.Cavalry, 20);
            cavalry.Morale = 5f;

            for (int i = 0; i < TicksFor(30f) && match.FindSquad(cavalry.Id) != null; i++)
            {
                match.Tick();
            }

            Assert.IsNull(match.FindSquad(cavalry.Id));
            Assert.AreEqual(20, match.GetPlayer(Side.France).CavalryReserve);
            Assert.AreEqual(0, match.GetPlayer(Side.France).GrenadierReserve);
            Assert.IsNotNull(stable);
        }

        [Test]
        public void Cavalry_CannotFire()
        {
            var match = new Match(1, TestSettings.Create());
            Squad cavalry = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line, UnitType.Cavalry);
            match.AddSquad(Side.Austria, new float2(0f, 5f), -90f, Formation.Line);
            match.Enqueue(new VolleyCommand(cavalry.Id));

            RunTicks(match, 30);

            Assert.IsFalse(VolleyRules.CanVolley(match, cavalry));
            Assert.AreEqual(FireState.Loaded, cavalry.FireState, "Конница не прицеливается");
            Assert.IsEmpty(match.LastTickVolleys);
        }

        [Test]
        public void Cavalry_HitsHarderInMelee_ThanGrenadiers()
        {
            int CasualtiesInflicted(UnitType attackerType)
            {
                var match = new Match(1, TestSettings.Create());
                Squad attacker = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line, attackerType, 24);
                Squad defender = match.AddSquad(Side.Austria, new float2(0f, 3f), -90f, Formation.Line, 36);
                defender.Morale = 100f;
                match.Enqueue(new ChargeCommand(attacker.Id, defender.Id));
                for (int i = 0; i < TicksFor(8f); i++)
                {
                    match.Tick();
                }

                return 36 - defender.Alive;
            }

            Assert.Greater(CasualtiesInflicted(UnitType.Cavalry), CasualtiesInflicted(UnitType.Grenadiers) * 1.8f);
        }
    }
}
