using System;
using Marchlands.Simulation.Core;
using NUnit.Framework;

namespace Marchlands.Tests.EditMode
{
    public class GameRandomTests
    {
        private const int SequenceLength = 10000;

        [TestCase(0UL)]
        [TestCase(1UL)]
        [TestCase(42UL)]
        [TestCase(ulong.MaxValue)]
        public void SameSeed_ProducesSameSequence(ulong seed)
        {
            var first = new GameRandom(seed);
            var second = new GameRandom(seed);

            for (int i = 0; i < SequenceLength; i++)
            {
                Assert.AreEqual(first.NextUInt(), second.NextUInt(), $"Расхождение на шаге {i}");
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            var first = new GameRandom(1);
            var second = new GameRandom(2);

            int equal = 0;
            for (int i = 0; i < SequenceLength; i++)
            {
                if (first.NextUInt() == second.NextUInt())
                {
                    equal++;
                }
            }

            Assert.Less(equal, 5);
        }

        [Test]
        public void KnownSeed_ProducesReferenceValues()
        {
            // Эталон посчитан независимой реализацией PCG32 и фиксирует алгоритм:
            // если он изменится, одинаковые seed начнут давать другие бои.
            var random = new GameRandom(42);
            uint[] actual = { random.NextUInt(), random.NextUInt(), random.NextUInt() };

            CollectionAssert.AreEqual(new[] { 3270867926u, 1795671209u, 1924641435u }, actual);
        }

        [Test]
        public void RestoredFromState_ContinuesSameSequence()
        {
            var original = new GameRandom(123);
            for (int i = 0; i < 100; i++)
            {
                original.NextUInt();
            }

            GameRandom restored = GameRandom.FromState(original.State);

            for (int i = 0; i < SequenceLength; i++)
            {
                Assert.AreEqual(original.NextUInt(), restored.NextUInt());
            }
        }

        [Test]
        public void NextFloat_StaysInUnitInterval()
        {
            var random = new GameRandom(7);
            for (int i = 0; i < SequenceLength; i++)
            {
                float value = random.NextFloat();
                Assert.That(value, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }

        [Test]
        public void NextFloat_MeanIsAboutHalf()
        {
            var random = new GameRandom(7);
            double sum = 0;
            for (int i = 0; i < SequenceLength; i++)
            {
                sum += random.NextFloat();
            }

            Assert.AreEqual(0.5, sum / SequenceLength, 0.02);
        }

        [Test]
        public void IntRange_StaysInBoundsAndHitsEveryValue()
        {
            var random = new GameRandom(9);
            var hits = new int[6];
            for (int i = 0; i < SequenceLength; i++)
            {
                int value = random.Range(-2, 4);
                Assert.That(value, Is.InRange(-2, 3));
                hits[value + 2]++;
            }

            foreach (int count in hits)
            {
                Assert.Greater(count, 0);
            }
        }

        [Test]
        public void FloatRange_StaysInBounds()
        {
            var random = new GameRandom(11);
            for (int i = 0; i < SequenceLength; i++)
            {
                float value = random.Range(3f, 7f);
                Assert.That(value, Is.GreaterThanOrEqualTo(3f).And.LessThan(7f));
            }
        }

        [Test]
        public void IntRange_WithEmptyInterval_Throws()
        {
            var random = new GameRandom(1);
            Assert.Throws<ArgumentOutOfRangeException>(() => random.Range(5, 5));
        }
    }
}
