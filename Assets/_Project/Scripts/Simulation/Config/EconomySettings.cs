using System;

namespace Marchlands.Simulation.Config
{
    /// <summary>Параметры деревень и дохода (раздел 3.5 и 6 docs/design.md).</summary>
    [Serializable]
    public sealed class EconomySettings
    {
        /// <summary>Радиус захвата деревни, u (от центра отряда до центра деревни).</summary>
        public float captureRadius;

        /// <summary>Время захвата, s.</summary>
        public float captureTime;

        /// <summary>Скорость убывания незавершённого захвата, секунд прогресса за секунду.</summary>
        public float captureDecayRate;

        /// <summary>Период начисления дохода, s.</summary>
        public float incomeInterval;

        /// <summary>Доход за каждую деревню за период.</summary>
        public int villageIncome;

        /// <summary>Доход за штаб за период.</summary>
        public int hqIncome;

        /// <summary>Золото на старте.</summary>
        public int startGold;
    }
}
