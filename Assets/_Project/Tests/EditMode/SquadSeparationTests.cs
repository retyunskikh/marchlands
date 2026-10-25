using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class SquadSeparationTests
    {
        private const float PenetrationTolerance = 1e-3f;

        private static void AssertNoOverlap(Match match, int tick)
        {
            SquadSettings settings = match.Settings.squads;
            for (int i = 0; i < match.Squads.Count; i++)
            {
                for (int j = i + 1; j < match.Squads.Count; j++)
                {
                    SquadFootprint a = SquadFootprint.Of(match.Squads[i], settings);
                    SquadFootprint b = SquadFootprint.Of(match.Squads[j], settings);
                    if (SquadFootprint.TryGetPenetration(a, b, out float2 push))
                    {
                        Assert.Less(math.length(push), PenetrationTolerance, $"Отряды {i} и {j} пересекаются на тике {tick}");
                    }
                }
            }
        }

        private static void RunAndCheck(Match match, int ticks)
        {
            for (int tick = 0; tick < ticks; tick++)
            {
                match.Tick();
                AssertNoOverlap(match, tick);
            }
        }

        [Test]
        public void HeadOnLines_DoNotPassThroughEachOther()
        {
            var match = new Match(1, TestSettings.Create());
            Squad west = match.AddSquad(Side.France, new float2(-10f, 0f), 0f, Formation.Line);
            Squad east = match.AddSquad(Side.France, new float2(10f, 0f), 180f, Formation.Line);
            match.Enqueue(new MoveCommand(west.Id, new float2(10f, 0f), 0f));
            match.Enqueue(new MoveCommand(east.Id, new float2(-10f, 0f), 180f));

            RunAndCheck(match, 400);

            Assert.Less(west.Position.x, east.Position.x, "Отряды прошли сквозь друг друга");
        }

        [Test]
        public void ColumnMarchingThroughLine_IsPushedAside()
        {
            var match = new Match(1, TestSettings.Create());
            match.AddSquad(Side.France, float2.zero, 0f, Formation.Line);
            Squad column = match.AddSquad(Side.France, new float2(0f, -10f), 90f, Formation.Column);
            match.Enqueue(new MoveCommand(column.Id, new float2(0.5f, 10f), 90f));

            RunAndCheck(match, 300);
        }

        [Test]
        public void SquadMarchingIntoStandingSquad_PushesItWithoutOverlap()
        {
            var match = new Match(1, TestSettings.Create());
            Squad standing = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            Squad marching = match.AddSquad(Side.France, new float2(0f, -6f), 90f, Formation.Line);
            match.Enqueue(new MoveCommand(marching.Id, new float2(0f, 2f), 90f));

            RunAndCheck(match, 200);

            Assert.Greater(standing.Position.y, 0f, "Стоящий отряд должен быть оттеснён");
        }

        [Test]
        public void DistantSquads_AreNotMoved()
        {
            var match = new Match(1, TestSettings.Create());
            Squad a = match.AddSquad(Side.France, new float2(-20f, 0f), 0f, Formation.Line);
            Squad b = match.AddSquad(Side.Austria, new float2(20f, 0f), 180f, Formation.Line);

            for (int i = 0; i < 50; i++)
            {
                match.Tick();
            }

            Assert.AreEqual(new float2(-20f, 0f), a.Position);
            Assert.AreEqual(new float2(20f, 0f), b.Position);
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
