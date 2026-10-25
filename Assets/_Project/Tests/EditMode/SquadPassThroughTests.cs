using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    /// <summary>Отряды проходят друг сквозь друга: отталкивания нет (раздел 3.1 docs/design.md).</summary>
    public class SquadPassThroughTests
    {
        [Test]
        public void HeadOnLines_PassThroughAndReachTheirTargets()
        {
            var match = new Match(1, TestSettings.Create());
            Squad west = match.AddSquad(Side.France, new float2(-10f, 0f), 0f, Formation.Line);
            Squad east = match.AddSquad(Side.France, new float2(10f, 0f), 180f, Formation.Line);
            match.Enqueue(new MoveCommand(west.Id, new float2(10f, 0f), 0f));
            match.Enqueue(new MoveCommand(east.Id, new float2(-10f, 0f), 180f));

            for (int tick = 0; tick < 400; tick++)
            {
                match.Tick();
            }

            Assert.AreEqual(10f, west.Position.x, 1e-3f);
            Assert.AreEqual(-10f, east.Position.x, 1e-3f);
        }

        [Test]
        public void SquadMarchesThroughStandingEnemy_WithoutPushingIt()
        {
            var match = new Match(1, TestSettings.Create());
            Squad standing = match.AddSquad(Side.Austria, float2.zero, 90f, Formation.Line);
            Squad marching = match.AddSquad(Side.France, new float2(0f, -10f), 90f, Formation.Column);
            match.Enqueue(new MoveCommand(marching.Id, new float2(0f, 10f), 90f));

            for (int tick = 0; tick < 200; tick++)
            {
                match.Tick();
            }

            Assert.AreEqual(float2.zero, standing.Position, "Стоящий отряд не сдвигается");
            Assert.AreEqual(10f, marching.Position.y, 1e-3f);
        }

        [Test]
        public void SameSeedAndCommands_GiveIdenticalState()
        {
            Match first = CreateCrowdedScenario();
            Match second = CreateCrowdedScenario();

            for (int tick = 0; tick < 300; tick++)
            {
                first.Tick();
                second.Tick();
            }

            for (int i = 0; i < first.Squads.Count; i++)
            {
                Assert.AreEqual(first.Squads[i].Position, second.Squads[i].Position);
                Assert.AreEqual(first.Squads[i].Facing, second.Squads[i].Facing);
            }
        }

        private static Match CreateCrowdedScenario()
        {
            var match = new Match(5, TestSettings.Create());
            for (int i = 0; i < 4; i++)
            {
                Squad squad = match.AddSquad(Side.France, new float2(i * 3f, -i * 4f), i * 40f, i % 2 == 0 ? Formation.Line : Formation.Column);
                match.Enqueue(new MoveCommand(squad.Id, new float2(5f - i, 5f + i), 90f));
            }

            match.Enqueue(20, new ChangeFormationCommand(1, Formation.Column));
            return match;
        }
    }
}
