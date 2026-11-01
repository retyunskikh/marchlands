using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    /// <summary>Марш по точкам маршрута (к мосту, по дороге): на изгибах не встаёт, к цели — встаёт фронтом.</summary>
    public class RouteFlowTests
    {
        [Test]
        public void Line_OnRouteBends_TurnsOnTheMove_WithoutStopping()
        {
            var match = new Match(1, TestSettings.Create());
            Squad squad = match.AddSquad(Side.France, float2.zero, 0f, Formation.Line);
            var goal = new float2(30f, 20f);
            match.Enqueue(new MoveCommand(squad.Id, goal, 0f));
            match.Tick();
            // Маршрут с изгибами по 30–45°, как у моста или дороги.
            squad.Route.Clear();
            squad.Route.Add(new float2(10f, 0f));
            squad.Route.Add(new float2(18f, 6f));
            squad.Route.Add(new float2(24f, 14f));
            squad.Route.Add(goal);
            squad.RouteIndex = 0;

            int standing = 0;
            float2 previous = squad.Position;
            for (int i = 0; i < 600 && squad.CurrentRouteIndex < squad.Route.Count - 1; i++)
            {
                match.Tick();
                if (math.distance(previous, squad.Position) < 1e-4f)
                {
                    standing++;
                }

                previous = squad.Position;
            }

            Assert.AreEqual(squad.Route.Count - 1, squad.CurrentRouteIndex, "дошёл до последнего отрезка");
            Assert.AreEqual(0, standing, "на изгибах маршрута линия не останавливается");
            Assert.AreEqual(Formation.Line, squad.Formation, "строй прежний");
        }

        [Test]
        public void RoutePoint_RightBeforeGoal_IsDropped()
        {
            var settings = TestSettings.Create();
            settings.squads.routeEndMergeDistance = 3f;
            var waypoints = new System.Collections.Generic.List<float2> { new float2(10f, 0f), new float2(20f, 0f), new float2(21.5f, 0.5f) };
            typeof(RoutePlanner).GetMethod("DropCrowdedEnd", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { new Match(1, settings), waypoints, settings.squads.routeEndMergeDistance });
            Assert.AreEqual(2, waypoints.Count);
            Assert.AreEqual(new float2(21.5f, 0.5f), waypoints[1], "цель осталась, лишняя точка у неё — нет");
        }
    }
}
