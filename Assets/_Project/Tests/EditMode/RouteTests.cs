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
