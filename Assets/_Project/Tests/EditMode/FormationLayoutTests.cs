using System.Collections.Generic;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class FormationLayoutTests
    {
        private SquadSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = TestSettings.Create().squads;
        }

        [Test]
        public void FullLine_IsTwoRanksOfEighteen()
        {
            FormationLayout.GetGrid(Formation.Line, 36, settings, out int files, out int ranks);

            Assert.AreEqual(18, files);
            Assert.AreEqual(2, ranks);
        }

        [Test]
        public void FullColumn_IsSixBySix()
        {
            FormationLayout.GetGrid(Formation.Column, 36, settings, out int files, out int ranks);

            Assert.AreEqual(6, files);
            Assert.AreEqual(6, ranks);
        }

        [Test]
        public void HalfExtents_MatchGridAndSpacing()
        {
            float2 line = FormationLayout.GetHalfExtents(Formation.Line, 36, settings);
            float2 column = FormationLayout.GetHalfExtents(Formation.Column, 36, settings);

            Assert.AreEqual(4.5f, line.x, 1e-5f);
            Assert.AreEqual(0.8f, line.y, 1e-5f);
            Assert.AreEqual(1.5f, column.x, 1e-5f);
            Assert.AreEqual(2.4f, column.y, 1e-5f);
        }

        [Test]
        public void SlotCount_EqualsAlive_InBothFormations([Values(Formation.Line, Formation.Column)] Formation formation)
        {
            var slots = new List<float2>();
            for (int alive = 1; alive <= 36; alive++)
            {
                FormationLayout.GetLocalSlots(formation, alive, settings, slots);
                Assert.AreEqual(alive, slots.Count, $"{formation}, alive = {alive}");
            }
        }

        [Test]
        public void Slots_AreDistinctAndInsideFootprint([Values(Formation.Line, Formation.Column)] Formation formation)
        {
            var slots = new List<float2>();
            for (int alive = 1; alive <= 36; alive++)
            {
                FormationLayout.GetLocalSlots(formation, alive, settings, slots);
                float2 half = FormationLayout.GetHalfExtents(formation, alive, settings);

                for (int i = 0; i < slots.Count; i++)
                {
                    Assert.LessOrEqual(math.abs(slots[i].x), half.x, $"{formation}, alive = {alive}, slot {i}");
                    Assert.LessOrEqual(math.abs(slots[i].y), half.y, $"{formation}, alive = {alive}, slot {i}");
                    for (int j = i + 1; j < slots.Count; j++)
                    {
                        Assert.Greater(math.distance(slots[i], slots[j]), 0.1f, $"{formation}, alive = {alive}, slots {i} и {j}");
                    }
                }
            }
        }

        [Test]
        public void FrontRank_ComesFirst()
        {
            var slots = new List<float2>();
            FormationLayout.GetLocalSlots(Formation.Line, 36, settings, slots);

            Assert.Greater(slots[0].y, slots[35].y);
        }

        [Test]
        public void LocalToWorld_RotatesWithFacing()
        {
            // Фронт смотрит вверх (90°): «вправо по фронту» — это +X мира, «вперёд» — +Y.
            float2 world = FormationLayout.LocalToWorld(new float2(1f, 2f), new float2(10f, 10f), 90f);

            Assert.AreEqual(11f, world.x, 1e-4f);
            Assert.AreEqual(12f, world.y, 1e-4f);
        }
    }
}
