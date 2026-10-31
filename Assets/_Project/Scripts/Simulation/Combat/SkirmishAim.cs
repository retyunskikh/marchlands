namespace Marchlands.Simulation.Combat
{
    /// <summary>
    /// Выстрел вразнобой, который ещё готовится: один человек толпы вскинул ружьё и выстрелит через <see cref="Remaining"/> секунд.
    /// </summary>
    public readonly struct SkirmishAim
    {
        public SkirmishAim(int shooterIndex, float remaining)
        {
            ShooterIndex = shooterIndex;
            Remaining = remaining;
        }

        /// <summary>Номер стрелка в толпе (по нему отображение выбирает фигурку); может превышать численность.</summary>
        public int ShooterIndex { get; }

        /// <summary>Сколько секунд осталось до выстрела.</summary>
        public float Remaining { get; }
    }
}
