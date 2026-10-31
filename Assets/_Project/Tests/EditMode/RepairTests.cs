using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class RepairTests
    {
        private static readonly float2 FranceHq = new float2(-30f, 0f);

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

        private static Match CreateMatch(out Building barracks)
        {
            var match = new Match(1, TestSettings.Create());
            match.AddHeadquarters(Side.France, FranceHq);
            barracks = match.AddBarracks(Side.France, FranceHq + new float2(8f, 0f));
            barracks.BuildRemaining = 0f;
            barracks.Health = 100f;
            return match;
        }

        [Test]
        public void DamagedBuilding_IsRepaired_AfterDelay_AtRepairRate()
        {
            Match match = CreateMatch(out Building barracks);

            RunTicks(match, TicksFor(15f) - 1);
            Assert.AreEqual(100f, barracks.Health, 1e-3f, "Первые repairDelay секунд не чинят");
            Assert.IsFalse(barracks.IsRepairing);

            RunTicks(match, TicksFor(10f) + 1);
            Assert.IsTrue(barracks.IsRepairing);
            Assert.AreEqual(150f, barracks.Health, 1f, "10 с × 5 в секунду");

            RunTicks(match, TicksFor(60f));
            Assert.AreEqual(barracks.MaxHealth, barracks.Health, "Чинят только до полного здоровья");
            Assert.IsFalse(barracks.IsRepairing);
        }

        [Test]
        public void Repair_WaitsWhileEnemyIsNear()
        {
            Match match = CreateMatch(out Building barracks);
            Squad enemy = match.AddSquad(Side.Austria, barracks.Position + new float2(11f, 0f), 180f, Formation.Line);

            RunTicks(match, TicksFor(20f));
            Assert.AreEqual(100f, barracks.Health, 1e-3f, "Враг ближе repairEnemyRadius — не чинят");

            enemy.Alive = 0;
            match.Tick();
            match.Tick();
            Assert.IsTrue(barracks.IsRepairing, "Враг ушёл, а урона давно не было — чинят сразу");
        }

        [Test]
        public void Assault_ResetsRepairDelay()
        {
            Match match = CreateMatch(out Building barracks);
            RunTicks(match, TicksFor(16f));
            Assert.IsTrue(barracks.IsRepairing);

            // Отряд бьёт здание секунду, затем уходит.
            Squad enemy = match.AddSquad(Side.Austria, barracks.Position + new float2(3f, 0f), 180f, Formation.Line, UnitType.Grenadiers, 10);
            match.Enqueue(new AssaultCommand(enemy.Id, barracks.Id));
            RunTicks(match, TicksFor(1f));
            Assert.IsFalse(barracks.IsRepairing);
            enemy.Alive = 0;
            match.Tick();
            float health = barracks.Health;

            RunTicks(match, TicksFor(10f));
            Assert.AreEqual(health, barracks.Health, 1e-3f, "После урона отсчёт repairDelay начинается заново");
        }

        [Test]
        public void WithoutHeadquarters_NobodyRepairs()
        {
            var match = new Match(1, TestSettings.Create());
            Building barracks = match.AddBarracks(Side.France, FranceHq);
            barracks.BuildRemaining = 0f;
            barracks.Health = 100f;

            RunTicks(match, TicksFor(30f));

            Assert.AreEqual(100f, barracks.Health, 1e-3f);
        }
    }
}
