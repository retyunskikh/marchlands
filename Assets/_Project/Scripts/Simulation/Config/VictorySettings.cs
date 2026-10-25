using System;

namespace Marchlands.Simulation.Config
{
    /// <summary>Параметры условий победы (раздел 3.7 и 6 docs/design.md).</summary>
    [Serializable]
    public sealed class VictorySettings
    {
        /// <summary>Радиус захвата штаба, u (от центра отряда).</summary>
        public float hqCaptureRadius;

        /// <summary>Сколько секунд нужно простоять у вражеского штаба, s.</summary>
        public float hqCaptureTime;

        /// <summary>Доля деревень для доминирования (округляется вверх до целого числа деревень).</summary>
        public float dominationShare;

        /// <summary>Сколько секунд нужно непрерывно держать эту долю деревень, s.</summary>
        public float dominationTime;
    }
}
