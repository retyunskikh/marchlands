using System.Collections.Generic;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Marchlands.Simulation.Terrain;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class RouteTests
    {
        private static int TicksFor(float seconds)
        {
            return (int)math.round(seconds * SimulationTime.TicksPerSecond);
        }

        /// <summary>
        /// Поле 120×60: дорога по y = 0 с запада на восток, между стартом и целью — широкий лес по y = 12.
        /// Отряд стоит на (−40, 12), цель — (40, 12): напрямую — через лес, по дороге — быстрее.
        /// </summary>
        private static Match ForestAndRoad(float roadMultiplier, out Squad squad)
        {
            var settings = TestSettings.Create();
            settings.terrain.roadSpeedMultiplier = roadMultiplier;
            var match = new Match(1, settings);
            var map = new TerrainMap(new float2(-60f, -30f), new float2(60f, 30f));
            map.AddRoad(new List<float2> { new float2(-60f, 0f), new float2(60f, 0f) });
            for (float x = -20f; x <= 20f; x += 6f)
            {
                map.AddForest(new float2(x, 12f), 5f);
            }

            match.SetTerrain(map);
            squad = match.AddSquad(Side.France, new float2(-40f, 12f), 0f, Formation.Line);
            return match;
        }

        [Test]
        public void RouteOnRoad_FollowsRoadAxisAndBends()
        {
            var settings = TestSettings.Create();
            settings.terrain.roadSpeedMultiplier = 1.5f;
            var match = new Match(1, settings);
            var map = new TerrainMap(new float2(-60f, -30f), new float2(60f, 30f));
            var bend = new float2(0f, 3f);
            map.AddRoad(new List<float2> { new float2(-60f, 0f), bend, new float2(60f, 0f) });
            for (float x = -20f; x <= 20f; x += 6f)
            {
                map.AddForest(new float2(x, 14f), 5f);
            }

            match.SetTerrain(map);
            Squad squad = match.AddSquad(Side.France, new float2(-40f, 12f), 0f, Formation.Line);
            match.Enqueue(new MoveCommand(squad.Id, new float2(40f, 12f), 0f, autoRoute: true));
            match.Tick();

            Assert.IsTrue(squad.HasRoute, "Маршрут проложен");
            bool passesBend = false;
            for (int i = 0; i < squad.RouteWaypoints.Count - 1; i++)
            {
                float2 point = squad.RouteWaypoints[i];
                // Путь прорежен: вершины не обязаны совпадать с опорной точкой, но линия пути идёт через изгиб.
                float2 next = squad.RouteWaypoints[i + 1];
                float2 along = next - point;
                float t = math.saturate(math.dot(bend - point, along) / math.max(1e-6f, math.lengthsq(along)));
                passesBend |= math.distance(point + along * t, bend) < 0.3f;
                if (map.IsOnRoad(point, settings.terrain.roadHalfWidth + settings.terrain.pathCellSize))
                {
                    Assert.IsTrue(map.IsOnRoad(point, 0.01f), $"Точка пути {point} — на оси дороги");
                }
            }

            Assert.IsTrue(passesBend, "Маршрут идёт через изгиб дороги");
        }

        [Test]
        public void WindingRoad_IsTaken_EvenIfSlowerInSameFormation()
        {
            var settings = TestSettings.Create();
            settings.terrain.roadSpeedMultiplier = 1.5f;
            var match = new Match(1, settings);
            var map = new TerrainMap(new float2(-60f, -50f), new float2(60f, 50f));
            map.AddRoad(new List<float2> { new float2(-60f, 0f), new float2(-40f, 0f), new float2(0f, 40f), new float2(40f, 0f), new float2(60f, 0f) });
            match.SetTerrain(map);
            Squad squad = match.AddSquad(Side.France, new float2(-40f, 0f), 0f, Formation.Line);
            match.Enqueue(new MoveCommand(squad.Id, new float2(40f, 0f), 0f, autoRoute: true));
            match.Tick();

            Assert.IsTrue(squad.HasRoute, "Петляющая дорога всё равно лучше, чем линией по полю");
            Assert.AreEqual(Formation.Column, squad.TargetFormation);
        }

        [Test]
        public void RoadPreference_PathFollowsRoad_EvenWhenFieldIsShorter()
        {
            var settings = TestSettings.Create();
            settings.terrain.roadSpeedMultiplier = 1.5f;
            settings.terrain.pathRoadPreference = 1.6f;
            var match = new Match(1, settings);
            var map = new TerrainMap(new float2(-60f, -50f), new float2(60f, 50f));
            // Дорога уходит дугой в сторону: по времени поле не хуже, но с предпочтением путь идёт по ней.
            map.AddRoad(new List<float2> { new float2(-50f, 0f), new float2(-30f, 0f), new float2(0f, 30f), new float2(30f, 0f), new float2(50f, 0f) });
            match.SetTerrain(map);
            Squad squad = match.AddSquad(Side.France, new float2(-30f, 0f), 0f, Formation.Line);
            match.Enqueue(new MoveCommand(squad.Id, new float2(30f, 0f), 0f, autoRoute: true));
            match.Tick();

            Assert.IsTrue(squad.HasRoute, "Пошли по дороге");
            bool viaBend = false;
            foreach (float2 point in squad.RouteWaypoints)
            {
                viaBend |= math.distance(point, new float2(0f, 30f)) < 0.5f;
            }

            Assert.IsTrue(viaBend, "Путь идёт по дуге дороги, а не напрямик полем");
        }

        [Test]
        public void Column_IsFiveAbreast_Tight()
        {
            var settings = TestSettings.Create();
            settings.squads.columnFrontWidth = 5;
            settings.squads.columnFileSpacing = 0.4f;
            FormationLayout.GetGrid(Formation.Column, 30, settings.squads, out int files, out int _);
            Assert.AreEqual(5, files, "По 5 в ряд");
            float2 half = FormationLayout.GetHalfExtents(Formation.Column, 30, settings.squads);
            Assert.AreEqual(1f, half.x, 1e-4f, "Плечом к плечу — по columnFileSpacing");
        }

        [Test]
        public void LongOrder_UsesRoad_MarchesInColumn_AndRestoresLine()
        {
            Match match = ForestAndRoad(1.5f, out Squad squad);
            match.Enqueue(new MoveCommand(squad.Id, new float2(40f, 12f), 0f, autoRoute: true));
            match.Tick();

            Assert.IsTrue(squad.HasRoute, "Маршрут проложен");
            Assert.AreEqual(Formation.Column, squad.TargetFormation, "Для марша — в колонну");

            bool onRoad = false;
            int ticks = 0;
            for (; ticks < TicksFor(200f) && squad.Order.Type == SquadOrderType.Move; ticks++)
            {
                match.Tick();
                onRoad |= match.Terrain.IsOnRoad(squad.Position, match.Settings.terrain.roadHalfWidth);
                Assert.IsFalse(match.Terrain.IsInForest(squad.Position), "Обходит лес");
            }

            Assert.IsTrue(onRoad, "Шёл по дороге");
            Assert.AreEqual(SquadOrderType.None, squad.Order.Type, "Дошёл");
            Assert.Less(math.distance(squad.Position, new float2(40f, 12f)), 0.2f);
            Assert.AreEqual(Formation.Line, squad.TargetFormation, "У цели — снова в линию");
            Assert.IsFalse(squad.HasRoute);
        }

        [Test]
        public void Route_IsFasterThanStraightLine()
        {
            Match routed = ForestAndRoad(1.5f, out Squad a);
            routed.Enqueue(new MoveCommand(a.Id, new float2(40f, 12f), 0f, autoRoute: true));
            Match straight = ForestAndRoad(1.5f, out Squad b);
            straight.Enqueue(new MoveCommand(b.Id, new float2(40f, 12f), 0f));

            int Run(Match match, Squad squad)
            {
                match.Tick();
                int ticks = 1;
                while (squad.Order.Type == SquadOrderType.Move && ticks < TicksFor(400f))
                {
                    match.Tick();
                    ticks++;
                }

                return ticks;
            }

            Assert.Less(Run(routed, a), Run(straight, b), "По дороге в колонне — быстрее, чем напрямую через лес в линии");
        }

        [Test]
        public void ShortOrder_GoesStraight()
        {
            Match match = ForestAndRoad(1.5f, out Squad squad);
            match.Enqueue(new MoveCommand(squad.Id, squad.Position + new float2(10f, 0f), 0f, autoRoute: true));
            match.Tick();

            Assert.IsFalse(squad.HasRoute);
            Assert.AreEqual(Formation.Line, squad.TargetFormation);
        }

        [Test]
        public void OpenField_StraightIsBetter_NoRoute()
        {
            var match = new Match(1, TestSettings.Create());
            match.SetTerrain(new TerrainMap(new float2(-60f, -30f), new float2(60f, 30f)));
            Squad squad = match.AddSquad(Side.France, new float2(-40f, 0f), 0f, Formation.Line);
            match.Enqueue(new MoveCommand(squad.Id, new float2(40f, 0f), 0f, autoRoute: true));
            match.Tick();

            Assert.IsFalse(squad.HasRoute, "В чистом поле дорога не нужна — напрямую");
            Assert.AreEqual(Formation.Line, squad.TargetFormation);
        }

        [Test]
        public void NewOrder_CancelsMarch()
        {
            Match match = ForestAndRoad(1.5f, out Squad squad);
            match.Enqueue(new MoveCommand(squad.Id, new float2(40f, 12f), 0f, autoRoute: true));
            match.Tick();
            Assert.IsTrue(squad.HasRoute);

            match.Enqueue(new StopCommand(squad.Id));
            match.Tick();

            Assert.IsFalse(squad.HasRoute);
        }

        [Test]
        public void AiOrders_NeverAutoRoute()
        {
            Match match = ForestAndRoad(1.5f, out Squad squad);
            match.Enqueue(new MoveCommand(squad.Id, new float2(40f, 12f), 0f));
            match.Tick();

            Assert.IsFalse(squad.HasRoute);
        }
    }
}
