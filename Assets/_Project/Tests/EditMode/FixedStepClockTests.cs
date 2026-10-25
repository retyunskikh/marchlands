using Marchlands.Simulation.Core;
using NUnit.Framework;

namespace Marchlands.Tests.EditMode
{
    public class FixedStepClockTests
    {
        private static FixedStepClock CreateClock()
        {
            return new FixedStepClock(SimulationTime.TicksPerSecond, SimulationTime.MaxTicksPerAdvance);
        }

        [Test]
        public void OneSecondInTickSizedFrames_GivesTenTicks()
        {
            FixedStepClock clock = CreateClock();

            int ticks = 0;
            for (int i = 0; i < SimulationTime.TicksPerSecond; i++)
            {
                ticks += clock.Advance(SimulationTime.TickDuration);
            }

            Assert.AreEqual(10, ticks);
        }

        [Test]
        public void TenSecondsAtSixtyFps_GivesHundredTicks()
        {
            FixedStepClock clock = CreateClock();

            int ticks = 0;
            for (int i = 0; i < 600; i++)
            {
                ticks += clock.Advance(1.0 / 60.0);
            }

            // Накопленная ошибка double может сдвинуть последний тик на следующий кадр.
            Assert.That(ticks, Is.InRange(99, 100));
        }

        [Test]
        public void ShortFrame_GivesNoTickAndPartialAlpha()
        {
            FixedStepClock clock = CreateClock();

            int ticks = clock.Advance(0.025);

            Assert.AreEqual(0, ticks);
            Assert.AreEqual(0.25f, clock.Alpha, 1e-5f);
        }

        [Test]
        public void Alpha_KeepsRemainderAfterTicks()
        {
            FixedStepClock clock = CreateClock();

            int ticks = clock.Advance(0.17);

            Assert.AreEqual(1, ticks);
            Assert.AreEqual(0.7f, clock.Alpha, 1e-5f);
        }

        [Test]
        public void LongFrame_IsClampedAndExcessDropped()
        {
            FixedStepClock clock = CreateClock();

            int ticks = clock.Advance(3.05);

            Assert.AreEqual(SimulationTime.MaxTicksPerAdvance, ticks);
            Assert.AreEqual(0.5f, clock.Alpha, 1e-4f);
            Assert.AreEqual(0, clock.Advance(0.0), "Отброшенные тики не должны догонять в следующих кадрах");
        }
    }
}
