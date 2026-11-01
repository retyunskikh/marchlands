using System.Collections.Generic;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class GroupMoveTests
    {
        [Test]
        public void SameFacing_KeepsOffsetsFromCenter()
        {
            var match = new Match(1, TestSettings.Create());
            var squads = new List<Squad>
            {
                match.AddSquad(Side.France, new float2(-10f, 0f), 90f, Formation.Line),
                match.AddSquad(Side.France, new float2(10f, 0f), 90f, Formation.Line),
                match.AddSquad(Side.France, new float2(0f, -6f), 90f, Formation.Line)
            };
            var targets = new List<float2>();
            GroupMove.Arrange(squads, new float2(30f, 40f), 90f, 90f, targets);

            float2 center = GroupMove.Center(squads);
            for (int i = 0; i < squads.Count; i++)
            {
                float2 expected = new float2(30f, 40f) + (squads[i].Position - center);
                Assert.Less(math.distance(targets[i], expected), 1e-4f);
            }
        }

        [Test]
        public void NewFacing_RotatesGroupAsAWhole()
        {
            var match = new Match(1, TestSettings.Create());
            // Две линии бок о бок фронтом на север (90°): левая и правая.
            var squads = new List<Squad>
            {
                match.AddSquad(Side.France, new float2(-10f, 0f), 90f, Formation.Line),
                match.AddSquad(Side.France, new float2(10f, 0f), 90f, Formation.Line)
            };
            var targets = new List<float2>();
            // Новый фронт — на восток (0°): левый фланг уходит на север, правый — на юг.
            GroupMove.Arrange(squads, float2.zero, 0f, 90f, targets);

            Assert.Less(math.distance(targets[0], new float2(0f, 10f)), 1e-3f, "Левый — севернее");
            Assert.Less(math.distance(targets[1], new float2(0f, -10f)), 1e-3f, "Правый — южнее");
        }

        [Test]
        public void Enqueue_OrdersEverySquad_WithCommonFacing()
        {
            var match = new Match(1, TestSettings.Create());
            var squads = new List<Squad>
            {
                match.AddSquad(Side.France, new float2(-8f, 0f), 90f, Formation.Line),
                match.AddSquad(Side.France, new float2(8f, 0f), 90f, Formation.Line)
            };
            GroupMove.Enqueue(match, squads, new float2(0f, 20f), 90f, 90f, autoRoute: false);
            match.Tick();

            foreach (Squad squad in squads)
            {
                Assert.AreEqual(SquadOrderType.Move, squad.Order.Type);
                Assert.AreEqual(90f, squad.Order.FinalFacing, 1e-3f);
            }

            Assert.Less(math.distance(squads[0].Order.Target, new float2(-8f, 20f)), 1e-3f);
            Assert.Less(math.distance(squads[1].Order.Target, new float2(8f, 20f)), 1e-3f);
        }
    }
}
