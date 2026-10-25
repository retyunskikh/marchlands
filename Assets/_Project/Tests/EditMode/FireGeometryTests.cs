using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class FireGeometryTests
    {
        [TestCase(0.5f)]
        [TestCase(3f)]
        [TestCase(7f)]
        [TestCase(11.9f)]
        public void FacingLines_DistanceIsBetweenFronts(float frontDistance)
        {
            Match match = CombatTestScenarios.FacingLines(1, frontDistance, out Squad shooter, out Squad target);

            Assert.AreEqual(frontDistance, FireGeometry.Distance(shooter, target, match.Settings.squads), 1e-4f);
        }

        [Test]
        public void SidewaysTarget_DistanceIsToItsNearestEdge()
        {
            var match = new Match(1, TestSettings.Create());
            Squad shooter = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            // Линия фронтом вправо: вдоль Y она тянется на 4.5 u в каждую сторону от центра.
            Squad target = match.AddSquad(Side.Austria, new float2(0f, 10f), 0f, Formation.Line);

            // Ближний край цели — y = 10 − 4.5, фронт стрелка — y = 0.8.
            Assert.AreEqual(10f - 4.5f - 0.8f, FireGeometry.Distance(shooter, target, match.Settings.squads), 1e-4f);
        }

        [Test]
        public void TargetBeyondFlank_IsMeasuredFromFrontEnd()
        {
            var match = new Match(1, TestSettings.Create());
            Squad shooter = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            // Колонна правее фронта: ближайшая точка фронта стрелка — его правый конец (4.5; 0.8).
            Squad target = match.AddSquad(Side.Austria, new float2(10f, 6f), -90f, Formation.Column);

            float2 nearestCorner = new float2(10f - 1.5f, 6f - 2.4f);
            float expected = math.distance(nearestCorner, new float2(4.5f, 0.8f));
            Assert.AreEqual(expected, FireGeometry.Distance(shooter, target, match.Settings.squads), 1e-4f);
        }

        [Test]
        public void OverlappingTarget_HasZeroDistance()
        {
            Match match = CombatTestScenarios.FacingLines(1, -0.3f, out Squad shooter, out Squad target);

            Assert.AreEqual(0f, FireGeometry.Distance(shooter, target, match.Settings.squads));
        }

        [TestCase(0f, 5f, true, TestName = "Sector_StraightAhead")]
        [TestCase(8.5f, 5f, true, TestName = "Sector_InsideWideningEdge")]
        [TestCase(9f, 5f, false, TestName = "Sector_OutsideWideningEdge")]
        [TestCase(0f, -3f, false, TestName = "Sector_Behind")]
        [TestCase(20f, 0.8f, false, TestName = "Sector_FlankOnFrontLine")]
        public void Sector_WidensFromFlanks(float x, float y, bool expected)
        {
            var match = new Match(1, TestSettings.Create());
            Squad shooter = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);

            // Фронт на y = 0.8, полуширина 4.5, tan 45° = 1: граница сектора |x| = 4.5 + (y − 0.8).
            Assert.AreEqual(expected, FireGeometry.IsInSector(shooter, new float2(x, y), match.Settings));
        }
    }
}
