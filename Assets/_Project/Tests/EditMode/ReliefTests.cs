using System.Collections.Generic;
using Marchlands.Simulation.AI;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using Marchlands.Simulation.Terrain;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class ReliefTests
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

        /// <summary>Поле 100×60, река с севера на юг по x = 0, брод на y = 20, мост на y = −20.</summary>
        private static TerrainMap RiverMap()
        {
            var map = new TerrainMap(new float2(-50f, -30f), new float2(50f, 30f));
            map.AddRiver(new List<float2> { new float2(0f, -40f), new float2(0f, 40f) }, 2.5f);
            map.AddCrossing(new float2(0.5f, 20f), CrossingType.Ford);
            map.AddCrossing(new float2(0f, -20f), CrossingType.Bridge);
            return map;
        }

        [Test]
        public void River_IsImpassable_ExceptCrossings()
        {
            TerrainMap map = RiverMap();
            var settings = TestSettings.Create().terrain;

            Assert.IsTrue(map.IsWater(new float2(0f, 0f)));
            Assert.AreEqual(0f, map.SpeedMultiplier(new float2(1f, 0f), settings));
            Assert.IsFalse(map.IsWater(new float2(0f, 20f)), "Брод проходим");
            Assert.AreEqual(settings.fordSpeedMultiplier, map.SpeedMultiplier(new float2(0f, 20f), settings));
            Assert.IsTrue(map.IsFord(new float2(0f, 20f)));
            Assert.AreEqual(settings.roadSpeedMultiplier, map.SpeedMultiplier(new float2(0f, -20f), settings), "Мост — как дорога");
            Assert.AreEqual(1f, map.SpeedMultiplier(new float2(10f, 0f), settings), "Поле");
        }

        [Test]
        public void Ford_ExposesTarget_ForestStillCovers()
        {
            TerrainMap map = RiverMap();
            map.AddForest(new float2(20f, 0f), 4f);
            var settings = TestSettings.Create().terrain;

            Assert.AreEqual(settings.fordTargetMultiplier, map.TargetHitMultiplier(new float2(0f, 20f), settings), 1e-5f);
            Assert.AreEqual(settings.forestCoverMultiplier, map.TargetHitMultiplier(new float2(20f, 0f), settings), 1e-5f);
            Assert.AreEqual(1f, map.TargetHitMultiplier(new float2(-20f, 0f), settings), 1e-5f);
        }

        [Test]
        public void Squad_OrderedAcrossRiver_GoesThroughCrossing_NeverEntersWater()
        {
            var match = new Match(1, TestSettings.Create());
            match.SetTerrain(RiverMap());
            Squad squad = match.AddSquad(Side.France, new float2(-15f, 5f), 0f, Formation.Column);
            match.Enqueue(new MoveCommand(squad.Id, new float2(15f, 5f), 0f));

            bool usedCrossing = false;
            for (int i = 0; i < TicksFor(120f); i++)
            {
                match.Tick();
                Assert.IsFalse(match.Terrain.IsWater(squad.Position), $"Отряд в воде на тике {i}: {squad.Position}");
                usedCrossing |= match.Terrain.IsFord(squad.Position) || match.Terrain.IsOnBridge(squad.Position);
                if (squad.Order.Type != SquadOrderType.Move)
                {
                    break;
                }
            }

            Assert.IsTrue(usedCrossing, "Шёл через переправу");
            Assert.AreEqual(SquadOrderType.None, squad.Order.Type, "Дошёл до цели");
            Assert.Less(math.distance(squad.Position, new float2(15f, 5f)), 0.2f);
        }

        [Test]
        public void MoveTarget_InWater_GoesToNearBank()
        {
            var match = new Match(1, TestSettings.Create());
            match.SetTerrain(RiverMap());
            Squad squad = match.AddSquad(Side.France, new float2(-15f, 0f), 0f, Formation.Column);
            match.Enqueue(new MoveCommand(squad.Id, new float2(0.5f, 0f), 0f));
            match.Tick();

            Assert.Less(squad.Order.Target.x, -2.5f, "Цель — на своём берегу");
            Assert.IsFalse(match.Terrain.IsWater(squad.Order.Target));
        }

        [Test]
        public void Ford_SlowsSquad()
        {
            var match = new Match(1, TestSettings.Create());
            match.SetTerrain(RiverMap());

            Assert.AreEqual(match.Settings.terrain.fordSpeedMultiplier, match.TerrainSpeedMultiplier(new float2(0f, 20f)));
        }

        [Test]
        public void Hill_ExtendsRange()
        {
            var settings = TestSettings.Create();
            var match = new Match(1, settings);
            var map = new TerrainMap(new float2(-50f, -30f), new float2(50f, 30f));
            map.AddHill(new float2(0f, 0f), 5f);
            match.SetTerrain(map);
            Squad onHill = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            Squad below = match.AddSquad(Side.France, new float2(-20f, 0f), 90f, Formation.Line);
            match.Tick();

            Assert.IsTrue(onHill.IsOnHill);
            Assert.IsFalse(below.IsOnHill);
            Assert.AreEqual(settings.combat.maxRange * settings.terrain.hillRangeMultiplier, UnitStats.MaxRange(onHill, settings), 1e-4f);
            Assert.AreEqual(settings.combat.maxRange, UnitStats.MaxRange(below, settings), 1e-4f);
        }

        [Test]
        public void Hill_ShooterReachesTargetBeyondNormalRange()
        {
            var settings = TestSettings.Create();
            var match = new Match(1, settings);
            var map = new TerrainMap(new float2(-50f, -30f), new float2(50f, 30f));
            map.AddHill(new float2(0f, 0f), 5f);
            match.SetTerrain(map);
            Squad shooter = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            Squad target = match.AddSquad(Side.Austria, new float2(0f, 30f), -90f, Formation.Line);
            match.Tick();

            // Ставим цель так, чтобы от фронта до неё было 1.1 × maxRange.
            float distance = FireGeometry.Distance(shooter, target, settings.squads);
            target.Position += new float2(0f, settings.combat.maxRange * 1.1f - distance);
            Assert.AreEqual(settings.combat.maxRange * 1.1f, FireGeometry.Distance(shooter, target, settings.squads), 1e-3f);

            Assert.IsTrue(VolleyRules.CanVolley(match, shooter), "С холма достаёт дальше maxRange");
        }

        [Test]
        public void Charge_Uphill_IsSlower()
        {
            float Covered(bool hill)
            {
                var match = new Match(1, TestSettings.Create());
                var map = new TerrainMap(new float2(-50f, -30f), new float2(50f, 30f));
                if (hill)
                {
                    map.AddHill(new float2(0f, 14f), 15f);
                }

                match.SetTerrain(map);
                Squad attacker = match.AddSquad(Side.France, float2.zero, 90f, Formation.Column);
                Squad target = match.AddSquad(Side.Austria, new float2(0f, 25f), -90f, Formation.Line);
                match.Enqueue(new ChargeCommand(attacker.Id, target.Id));
                RunTicks(match, TicksFor(2f));
                return attacker.Position.y;
            }

            Assert.AreEqual(Covered(false) * TestSettings.Create().terrain.uphillChargeMultiplier, Covered(true), 0.15f);
        }

        [Test]
        public void Ai_CrossesRiverThroughCrossing_ToCaptureVillage()
        {
            var match = new Match(1, TestSettings.Create());
            match.SetTerrain(RiverMap());
            Village village = match.AddVillage(new float2(20f, 0f));
            Squad squad = match.AddSquad(Side.Austria, new float2(-20f, 0f), 0f, Formation.Column);
            match.AddController(new TacticalAi(Side.Austria));

            for (int i = 0; i < TicksFor(180f) && village.Owner != Side.Austria; i++)
            {
                match.Tick();
                Assert.IsFalse(match.Terrain.IsWater(squad.Position));
            }

            Assert.AreEqual(Side.Austria, village.Owner, "ИИ переправился и взял деревню");
        }
    }
}
