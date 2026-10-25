using System;

namespace Marchlands.Simulation.Core
{
    /// <summary>
    /// Переводит реальное время кадров в целое число тиков симуляции и даёт коэффициент интерполяции
    /// для отображения между последним и следующим тиком.
    /// </summary>
    public sealed class FixedStepClock
    {
        private readonly int ticksPerSecond;
        private readonly int maxTicksPerAdvance;

        // Накопленное время хранится в тиках, а не в секундах: так шаг 1/10 с складывается без ошибки округления.
        private double accumulatedTicks;

        public FixedStepClock(int ticksPerSecond, int maxTicksPerAdvance)
        {
            if (ticksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            }

            if (maxTicksPerAdvance <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTicksPerAdvance));
            }

            this.ticksPerSecond = ticksPerSecond;
            this.maxTicksPerAdvance = maxTicksPerAdvance;
        }

        /// <summary>Доля шага, прошедшая после последнего тика, в [0, 1).</summary>
        public float Alpha => (float)accumulatedTicks;

        /// <summary>Добавляет время кадра и возвращает, сколько тиков нужно выполнить.</summary>
        public int Advance(double deltaSeconds)
        {
            if (deltaSeconds < 0 || double.IsNaN(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            accumulatedTicks += deltaSeconds * ticksPerSecond;
            int ticks = (int)Math.Floor(accumulatedTicks);
            accumulatedTicks -= ticks;

            return Math.Min(ticks, maxTicksPerAdvance);
        }
    }
}
