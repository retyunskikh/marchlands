using System.Collections.Generic;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class BlockhouseTests
    {
        private static readonly float2 Position = new float2(30f, 0f);

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

        [Test]
        public void Blockhouse_IsBuiltAtStart_AndCannotHire()
        {
            var match = new Match(1, TestSettings.Create());
            Building blockhouse = match.AddBlockhouse(Side.Austria, Position);

            Assert.IsTrue(blockhouse.IsBuilt);
            Assert.AreEqual(match.Settings.buildings.blockhouseHealth, blockhouse.MaxHealth);
            Assert.IsFalse(BuildRules.CanHire(blockhouse));
            Assert.IsFalse(BuildRules.CanHireType(blockhouse, UnitType.Fusiliers));
        }

        [Test]
        public void Blockhouse_FiresStrongerThanSentries()
        {
            var settings = TestSettings.Create();
            float Kills(bool blockhouse)
            {
                var match = new Match(7, settings);
                if (blockhouse)
                {
                    match.AddBlockhouse(Side.Austria, Position);
                }
                else
                {
                    match.AddHeadquarters(Side.Austria, Position);
                }

                Squad target = match.AddSquad(Side.France, Position + new float2(-6f, 0f), 0f, Formation.Line, UnitType.Grenadiers, 100000);
                var events = new List<SentryVolley>();
                for (int i = 0; i < TicksFor(600f); i++)
                {
                    SentrySystem.Update(match, SimulationTime.TickDuration, events);
                }

                Assert.IsTrue(events.TrueForAll(v => v.Shooters == (blockhouse ? settings.buildings.blockhouseShots : settings.buildings.sentryShots)));
                return 100000 - target.Alive;
            }

            float sentries = Kills(false);
            float blockhouses = Kills(true);
            float expected = 600f / settings.buildings.blockhouseInterval * settings.buildings.blockhouseShots * settings.buildings.blockhouseHitChance;
            Assert.Greater(blockhouses, sentries * 1.3f, "Блокгауз бьёт заметно сильнее часовых");
            Assert.That(blockhouses, Is.EqualTo(expected).Within(expected * 0.15f));
        }

        [Test]
        public void Blockhouse_ReachesFartherThanSentries()
        {
            var settings = TestSettings.Create();
            var match = new Match(1, settings);
            match.AddBlockhouse(Side.Austria, Position);
            float distance = (settings.buildings.sentryRange + settings.buildings.blockhouseRange) * 0.5f;
            Squad target = match.AddSquad(Side.France, Position + new float2(-distance, 0f), 0f, Formation.Line, UnitType.Grenadiers, 1000);

            RunTicks(match, TicksFor(30f));

            Assert.Less(target.Alive, 1000, "Дальше часовых, но в blockhouseRange — стреляет");
        }

        [Test]
        public void Blockhouse_CanBeDestroyedByAssault()
        {
            var match = new Match(1, TestSettings.Create());
            Building blockhouse = match.AddBlockhouse(Side.Austria, Position);
            Squad squad = match.AddSquad(Side.France, Position + new float2(-8f, 0f), 0f, Formation.Column, UnitType.Grenadiers, 100000);
            match.Enqueue(new AssaultCommand(squad.Id, blockhouse.Id));

            RunTicks(match, TicksFor(30f));

            Assert.IsNull(match.FindBuilding(blockhouse.Id), "Разрушенный блокгауз исчезает с карты");
        }

        [Test]
        public void Blockhouse_IsRepairedByWorkers_OnlyWithoutEnemiesNear()
        {
            var settings = TestSettings.Create();
            var match = new Match(1, settings);
            match.AddHeadquarters(Side.Austria, new float2(50f, 0f));
            Building blockhouse = match.AddBlockhouse(Side.Austria, Position);
            blockhouse.Health = blockhouse.MaxHealth * 0.5f;
            // Огромный отряд: от огня блокгауза не побежит и остаётся угрозой рядом.
            Squad enemy = match.AddSquad(Side.France, Position + new float2(-settings.buildings.repairEnemyRadius * 0.5f, 0f), 0f,
                Formation.Line, UnitType.Grenadiers, 100000);

            RunTicks(match, TicksFor(settings.buildings.repairDelay + 5f));
            Assert.IsFalse(blockhouse.IsRepairing, "Пока враг рядом, рабочие не чинят");
            Assert.AreEqual(blockhouse.MaxHealth * 0.5f, blockhouse.Health, 1e-3f);

            enemy.Position = new float2(-40f, 0f);
            RunTicks(match, TicksFor(settings.buildings.repairDelay + 5f));
            Assert.IsTrue(blockhouse.IsRepairing || blockhouse.Health >= blockhouse.MaxHealth);
            Assert.Greater(blockhouse.Health, blockhouse.MaxHealth * 0.5f);
        }

        [Test]
        public void DestroyedBlockhouse_IsRebuilt_OnceBaseIsRetaken()
        {
            var settings = TestSettings.Create();
            var match = new Match(1, settings);
            match.AddHeadquarters(Side.Austria, new float2(50f, 0f));
            Building blockhouse = match.AddBlockhouse(Side.Austria, Position);
            Squad enemy = match.AddSquad(Side.France, Position + new float2(-settings.buildings.repairEnemyRadius * 0.5f, 0f), 0f,
                Formation.Line, UnitType.Grenadiers, 100000);
            match.DestroyBuilding(blockhouse);
            Assert.AreEqual(1, match.BlockhouseRuins.Count, "Остались руины");

            RunTicks(match, TicksFor(settings.buildings.repairDelay + 5f));
            Assert.IsNull(FindBlockhouse(match), "Пока враг у руин, не отстраивают");

            enemy.Position = new float2(-40f, 0f);
            RunTicks(match, TicksFor(settings.buildings.repairDelay + 1f));
            Building rebuilt = FindBlockhouse(match);
            Assert.IsNotNull(rebuilt, "Базу отбили — заложили блокгауз");
            Assert.AreEqual(0, match.BlockhouseRuins.Count);
            Assert.IsFalse(rebuilt.IsBuilt, "Его ещё строят");
            Assert.IsTrue(rebuilt.HasWorkers, "Строят рабочие из штаба");

            // Враг вернулся — стройка стоит.
            enemy.Position = Position + new float2(-settings.buildings.repairEnemyRadius * 0.5f, 0f);
            float remaining = rebuilt.BuildRemaining;
            RunTicks(match, TicksFor(5f));
            Assert.AreEqual(remaining, rebuilt.BuildRemaining, 0.11f, "При враге рядом не строят");

            enemy.Position = new float2(-40f, 0f);
            RunTicks(match, TicksFor(settings.buildings.blockhouseRebuildTime + 1f));
            Assert.IsTrue(rebuilt.IsBuilt, "Отстроили");
            Assert.AreEqual(rebuilt.MaxHealth, rebuilt.Health, 1e-3f);
        }

        private static Building FindBlockhouse(Match match)
        {
            foreach (Building building in match.Buildings)
            {
                if (building.Type == BuildingType.Blockhouse)
                {
                    return building;
                }
            }

            return null;
        }
    }
}
