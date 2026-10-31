using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using NUnit.Framework;

namespace Marchlands.Tests.EditMode
{
    public class VolleyMathTests
    {
        private SimulationSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = TestSettings.Create();
        }

        [TestCase(0f, 0.40f, TestName = "HitChance_Contact")]
        [TestCase(3f, 0.40f, TestName = "HitChance_PointBlankBoundary")]
        [TestCase(5f, 0.30f, TestName = "HitChance_BetweenPointBlankAndEffective")]
        [TestCase(7f, 0.20f, TestName = "HitChance_EffectiveBoundary")]
        [TestCase(9.5f, 0.125f, TestName = "HitChance_BetweenEffectiveAndMax")]
        [TestCase(12f, 0.05f, TestName = "HitChance_MaxBoundary")]
        [TestCase(12.5f, 0f, TestName = "HitChance_BeyondMax")]
        public void HitChance_FollowsPiecewiseLerp(float distance, float expected)
        {
            Assert.AreEqual(expected, VolleyMath.HitChance(distance, settings.combat), 1e-5f);
        }

        [Test]
        public void HitChance_DecreasesWithDistance()
        {
            float previous = float.MaxValue;
            for (float d = 0f; d <= 12f; d += 0.25f)
            {
                float chance = VolleyMath.HitChance(d, settings.combat);
                Assert.LessOrEqual(chance, previous, $"d = {d}");
                previous = chance;
            }
        }

        [Test]
        public void HitProbability_AppliesFirstVolleyAndColumnMultipliers()
        {
            CombatSettings combat = settings.combat;

            Assert.AreEqual(0.20f, VolleyMath.HitProbability(7f, false, Formation.Line, combat), 1e-5f);
            Assert.AreEqual(0.30f, VolleyMath.HitProbability(7f, true, Formation.Line, combat), 1e-5f);
            Assert.AreEqual(0.26f, VolleyMath.HitProbability(7f, false, Formation.Column, combat), 1e-5f);
            Assert.AreEqual(0.39f, VolleyMath.HitProbability(7f, true, Formation.Column, combat), 1e-5f);
        }

        [Test]
        public void HitProbability_NeverExceedsOne()
        {
            settings.combat.firstVolleyMultiplier = 5f;

            Assert.AreEqual(1f, VolleyMath.HitProbability(0f, true, Formation.Column, settings.combat));
        }

        [TestCase(Formation.Line, 36, 36)]
        [TestCase(Formation.Column, 36, 36)]
        [TestCase(Formation.Column, 4, 4)]
        public void Shooters_AllFire_InLineAndColumn(Formation formation, int alive, int expected)
        {
            Assert.AreEqual(expected, VolleyMath.Shooters(formation, alive, settings.squads));
        }

        [Test]
        public void RollCasualties_NeverExceedsTargetAlive()
        {
            var random = new GameRandom(3);
            for (int i = 0; i < 100; i++)
            {
                Assert.LessOrEqual(VolleyMath.RollCasualties(random, 36, 1f, 5), 5);
            }
        }

        [Test]
        public void RollCasualties_IsDeterministicForSeed()
        {
            var first = new GameRandom(99);
            var second = new GameRandom(99);
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(VolleyMath.RollCasualties(first, 36, 0.3f, 36), VolleyMath.RollCasualties(second, 36, 0.3f, 36));
            }
        }
    }
}
