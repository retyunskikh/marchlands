using System.Collections.Generic;
using System.Linq;
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
    public class TerrainTests
    {
        private static Match CreateMatch(TerrainMap terrain)
        {
            var match = new Match(1, TestSettings.Create());
            match.SetTerrain(terrain);
            return match;
        }

        private static float StepAt(Match match, Squad squad)
        {
            float2 before = squad.Position;
            match.Tick();
            return math.distance(before, squad.Position);
        }

        [Test]
        public void Forest_SlowsMarch()
        {
            var terrain = new TerrainMap(new float2(-50f, -50f), new float2(50f, 50f));
            terrain.AddForest(new float2(10f, 0f), 5f);
            Match match = CreateMatch(terrain);
            Squad squad = match.AddSquad(Side.France, new float2(9f, 0f), 0f, Formation.Line);
            match.Enqueue(new MoveCommand(squad.Id, new float2(40f, 0f), 0f));
            match.Tick();

            Assert.AreEqual(0.06f, StepAt(match, squad), 1e-4f, "1 u/s × 0.6 × 0.1 s");
        }

        [Test]
        public void Road_SpeedsUpMarch()
        {
            var terrain = new TerrainMap(new float2(-50f, -50f), new float2(50f, 50f));
            terrain.AddRoad(new[] { new float2(-40f, 0f), new float2(40f, 0f) });
            Match match = CreateMatch(terrain);
            Squad squad = match.AddSquad(Side.France, float2.zero, 0f, Formation.Line);
            match.Enqueue(new MoveCommand(squad.Id, new float2(30f, 0f), 0f));
            match.Tick();

            Assert.AreEqual(0.125f, StepAt(match, squad), 1e-4f, "1 u/s × 1.25 × 0.1 s");
        }

        [Test]
        public void OpenField_HasNormalSpeed()
        {
            var terrain = new TerrainMap(new float2(-50f, -50f), new float2(50f, 50f));
            Match match = CreateMatch(terrain);
            Squad squad = match.AddSquad(Side.France, float2.zero, 0f, Formation.Line);
            match.Enqueue(new MoveCommand(squad.Id, new float2(30f, 0f), 0f));
            match.Tick();

            Assert.AreEqual(0.1f, StepAt(match, squad), 1e-4f);
        }

        [Test]
        public void MoveBeyondBorder_StopsAtTheEdge()
        {
            var terrain = new TerrainMap(new float2(-20f, -10f), new float2(20f, 10f));
            Match match = CreateMatch(terrain);
            Squad squad = match.AddSquad(Side.France, float2.zero, 0f, Formation.Line);
            match.Enqueue(new MoveCommand(squad.Id, new float2(50f, 0f), 0f));

            for (int i = 0; i < 400; i++)
            {
                match.Tick();
            }

            // Фронтом вправо линия занимает по X лишь свою глубину: 0.8 u от центра.
            Assert.AreEqual(20f - 0.8f, squad.Position.x, 1e-3f);
            Assert.AreEqual(SquadOrderType.None, squad.Order.Type);
        }

        [Test]
        public void SquadNeverLeavesTheField()
        {
            var terrain = new TerrainMap(new float2(-20f, -10f), new float2(20f, 10f));
            Match match = CreateMatch(terrain);
            Squad squad = match.AddSquad(Side.France, new float2(0f, 8f), 90f, Formation.Line);
            squad.Morale = 5f; // бежит прочь — точки отхода нет, пусть упирается в край

            for (int i = 0; i < 200; i++)
            {
                match.Tick();
                Assert.IsTrue(terrain.Contains(squad.Position), $"Тик {i}");
            }
        }

        [Test]
        public void Forest_GivesCover()
        {
            Assert.AreEqual(0.14f, VolleyMath.HitProbability(7f, false, Formation.Line, TestSettings.Create().combat, 0.7f), 1e-5f);

            long total = 0;
            const int seeds = 500;
            for (ulong seed = 1; seed <= seeds; seed++)
            {
                Match match = CombatTestScenarios.FacingLines(seed, 7f, out Squad shooter, out Squad target);
                var terrain = new TerrainMap(new float2(-50f, -50f), new float2(50f, 50f));
                terrain.AddForest(target.Position, 6f);
                match.SetTerrain(terrain);
                total += CombatTestScenarios.FireOnce(match, shooter);
            }

            // 36 × 0.20 × 1.5 × 0.7 ≈ 7.6 вместо 10.8 на открытом месте.
            Assert.That((double)total / seeds, Is.InRange(6.6, 8.6));
        }

        [Test]
        public void Pathfinder_GoesAroundForest_WhenDetourIsFaster()
        {
            var terrain = new TerrainMap(new float2(-20f, -12f), new float2(20f, 12f));
            terrain.AddForest(float2.zero, 5f);
            var pathfinder = new TerrainPathfinder(terrain, TestSettings.Create().terrain);
            var path = new List<float2>();

            Assert.IsTrue(pathfinder.FindPath(new float2(-15f, 0f), new float2(15f, 0f), path));

            Assert.AreEqual(new float2(15f, 0f), path.Last());
            AssertPathAvoidsForest(terrain, new float2(-15f, 0f), path);
            float viaPath = PathCost(pathfinder, new float2(-15f, 0f), path);
            Assert.Less(viaPath, pathfinder.SegmentCost(new float2(-15f, 0f), new float2(15f, 0f)), "Обход дешевле, чем через лес");
        }

        [Test]
        public void Pathfinder_UsesRoad_WhenItIsFaster()
        {
            var terrain = new TerrainMap(new float2(-25f, -12f), new float2(25f, 12f));
            terrain.AddRoad(new[] { new float2(-24f, 9f), new float2(24f, 9f) });
            var pathfinder = new TerrainPathfinder(terrain, TestSettings.Create().terrain);
            var path = new List<float2>();
            var from = new float2(-20f, 6f);
            var to = new float2(20f, 6f);

            Assert.IsTrue(pathfinder.FindPath(from, to, path));

            Assert.Less(PathCost(pathfinder, from, path), pathfinder.SegmentCost(from, to), "По дороге быстрее, чем напрямую");
            Assert.IsTrue(path.Take(path.Count - 1).Any(p => terrain.IsOnRoad(p, 1.5f)), "Путь выходит на дорогу");
        }

        [Test]
        public void Pathfinder_IsDeterministic()
        {
            var terrain = new TerrainMap(new float2(-20f, -12f), new float2(20f, 12f));
            terrain.AddForest(new float2(3f, 1f), 4f);
            terrain.AddRoad(new[] { new float2(-18f, -8f), new float2(18f, 8f) });
            var first = new List<float2>();
            var second = new List<float2>();

            new TerrainPathfinder(terrain, TestSettings.Create().terrain).FindPath(new float2(-15f, 5f), new float2(15f, -5f), first);
            new TerrainPathfinder(terrain, TestSettings.Create().terrain).FindPath(new float2(-15f, 5f), new float2(15f, -5f), second);

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void AiSquad_WalksAroundForestToVillage()
        {
            var terrain = new TerrainMap(new float2(-30f, -15f), new float2(30f, 15f));
            terrain.AddForest(float2.zero, 6f);
            Match match = CreateMatch(terrain);
            Village village = match.AddVillage(new float2(20f, 0f));
            Squad squad = match.AddSquad(Side.Austria, new float2(-20f, 0f), 0f, Formation.Line);
            match.AddController(new TacticalAi(Side.Austria));

            int ticks = 0;
            while (village.Owner != Side.Austria)
            {
                match.Tick();
                ticks++;
                Assert.Less(ticks, 1500, "ИИ так и не дошёл до деревни");
                Assert.IsFalse(terrain.IsInForest(squad.Position), $"ИИ полез в лес на тике {ticks}");
            }
        }

        private static void AssertPathAvoidsForest(TerrainMap terrain, float2 from, List<float2> path)
        {
            float2 previous = from;
            foreach (float2 point in path)
            {
                for (int i = 0; i <= 20; i++)
                {
                    float2 sample = math.lerp(previous, point, i / 20f);
                    Assert.IsFalse(terrain.IsInForest(sample), $"Путь проходит через лес в {sample}");
                }

                previous = point;
            }
        }

        private static float PathCost(TerrainPathfinder pathfinder, float2 from, List<float2> path)
        {
            float cost = 0f;
            float2 previous = from;
            foreach (float2 point in path)
            {
                cost += pathfinder.SegmentCost(previous, point);
                previous = point;
            }

            return cost;
        }
    }
}
