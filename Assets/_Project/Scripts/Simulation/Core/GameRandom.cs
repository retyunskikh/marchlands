using System;

namespace Marchlands.Simulation.Core
{
    /// <summary>
    /// Детерминированный генератор случайных чисел (PCG32 XSH-RR).
    /// Единственный источник случайности в симуляции: одинаковый seed даёт одинаковую последовательность
    /// на любой платформе. Всё состояние — одно 64-битное поле, поэтому генератор легко сериализовать.
    /// </summary>
    [Serializable]
    public sealed class GameRandom
    {
        // Константы алгоритма PCG32 (O'Neill, pcg-random.org), а не параметры баланса.
        private const ulong Multiplier = 6364136223846793005UL;
        private const ulong Increment = 1442695040888963407UL;
        private const float FloatUnit = 1f / (1 << 24);

        private ulong state;

        public GameRandom(ulong seed)
        {
            state = 0;
            NextUInt();
            state += seed;
            NextUInt();
        }

        private GameRandom()
        {
        }

        /// <summary>Текущее внутреннее состояние; вместе с <see cref="FromState"/> позволяет сохранить и восстановить генератор.</summary>
        public ulong State => state;

        public static GameRandom FromState(ulong state)
        {
            return new GameRandom { state = state };
        }

        public uint NextUInt()
        {
            ulong old = state;
            state = unchecked(old * Multiplier + Increment);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rotation = (int)(old >> 59);
            return (xorShifted >> rotation) | (xorShifted << (-rotation & 31));
        }

        /// <summary>Равномерное число в [0, 1).</summary>
        public float NextFloat()
        {
            // Старшие 24 бита ровно помещаются в мантиссу float, поэтому результат строго меньше 1.
            return (NextUInt() >> 8) * FloatUnit;
        }

        /// <summary>Равномерное целое в [minInclusive, maxExclusive) без смещения распределения.</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive должен быть больше minInclusive.");
            }

            uint range = (uint)(maxExclusive - minInclusive);
            uint threshold = unchecked(0u - range) % range;
            while (true)
            {
                ulong product = (ulong)NextUInt() * range;
                if ((uint)product >= threshold)
                {
                    return minInclusive + (int)(product >> 32);
                }
            }
        }

        /// <summary>Равномерное число в [minInclusive, maxExclusive).</summary>
        public float Range(float minInclusive, float maxExclusive)
        {
            if (maxExclusive < minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive не может быть меньше minInclusive.");
            }

            float value = minInclusive + (maxExclusive - minInclusive) * NextFloat();
            // Округление float может дать ровно верхнюю границу — возвращаем её только для пустого интервала.
            return value < maxExclusive || maxExclusive == minInclusive ? value : minInclusive;
        }
    }
}
