using System.Collections.Generic;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Marchlands.Simulation.Terrain;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    /// <summary>Дороги и реки в симуляции — та же плавная кривая, что на экране.</summary>
    public class RoadCurveTests
    {
        [Test]
        public void Spline_PassesThroughControlPoints_AndIsDense()
        {
            var points = new List<float2> { new float2(0f, 0f), new float2(10f, 0f), new float2(10f, 10f) };
            float2[] line = Spline.Smooth(points, 0.5f);

            Assert.Greater(line.Length, 30, "Кривая подробная, а не три точки");
            foreach (float2 control in points)
            {
                float best = float.MaxValue;
                foreach (float2 point in line)
                {
                    best = math.min(best, math.distance(point, control));
                }

                Assert.Less(best, 1e-3f, $"Кривая проходит через опорную точку {control}");
            }
        }

        [Test]
        public void TerrainRoad_IsTheCurve_NotTheChord()
        {
            var map = new TerrainMap(new float2(-50f, -50f), new float2(50f, 50f));
            map.AddRoad(new List<float2> { new float2(-30f, 0f), new float2(0f, 0f), new float2(0f, 30f) });
            float2[] curve = Spline.Smooth(new List<float2> { new float2(-30f, 0f), new float2(0f, 0f), new float2(0f, 30f) }, 0.5f);

            // Каждая точка нарисованной кривой — на дороге симуляции.
            foreach (float2 point in curve)
            {
                Assert.IsTrue(map.IsOnRoad(point, 0.05f), $"Точка кривой {point} на дороге");
            }
        }

        [Test]
        public void RoadAlongForestEdge_RouteStaysOnRoad_AndArrives()
        {
            var settings = TestSettings.Create();
            settings.terrain.roadSpeedMultiplier = 1.5f;
            var match = new Match(1, settings);
            var map = new TerrainMap(new float2(-60f, -20f), new float2(60f, 20f));
            map.AddRoad(new List<float2> { new float2(-60f, 0f), new float2(60f, 0f) });
            // Роща вплотную к дороге: её край в 1.5 u от оси — запас от леса для пути накрыл бы дорогу.
            map.AddForest(new float2(0f, -6.5f), 5f);
            match.SetTerrain(map);
            Squad squad = match.AddSquad(Side.France, new float2(-45f, 0f), 0f, Formation.Column);
            var target = new float2(45f, 0f);
            match.Enqueue(new MoveCommand(squad.Id, target, 0f, autoRoute: true));
            match.Tick();

            foreach (float2 point in squad.RouteWaypoints)
            {
                Assert.IsTrue(map.IsOnRoad(point, 0.3f), $"Точка пути {point} на дороге, а не в обход рощи по полю");
            }

            int i = 0;
            for (; i < 2000 && squad.Order.Type != SquadOrderType.None; i++)
            {
                match.Tick();
            }

            Assert.AreEqual(SquadOrderType.None, squad.Order.Type, "Дошёл");
            Assert.Less(math.distance(squad.Position, target), 0.5f);
        }

        [Test]
        public void OrderIntoRiverBesideBridge_IsMovedToBank()
        {
            var match = new Match(1, TestSettings.Create());
            var map = new TerrainMap(new float2(-50f, -30f), new float2(50f, 30f));
            map.AddRoad(new List<float2> { new float2(-50f, 0f), new float2(50f, 0f) });
            map.AddRiver(new List<float2> { new float2(0f, -40f), new float2(0f, 40f) }, 2f);
            map.AddCrossing(new float2(0f, 0f), CrossingType.Bridge);
            match.SetTerrain(map);
            Squad squad = match.AddSquad(Side.France, new float2(-15f, 3f), 0f, Formation.Line);

            // Точка в русле рядом с мостом (в его круге, но не на настиле).
            var inRiver = new float2(0.5f, 3f);
            match.Enqueue(new MoveCommand(squad.Id, inRiver, 0f));
            match.Tick();

            Assert.Greater(math.abs(squad.Order.Target.x), 2f, "Цель перенесена из русла на берег");
            Assert.Less(squad.Order.Target.x, 0f, "На тот берег, где стоит отряд");
        }
    }
}
