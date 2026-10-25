namespace Marchlands.Simulation.Core
{
    /// <summary>
    /// Архитектурные константы времени симуляции. Это не параметры баланса: на фиксированный шаг
    /// опирается вся логика, поэтому он не настраивается в конфигах.
    /// </summary>
    public static class SimulationTime
    {
        public const int TicksPerSecond = 10;
        public const float TickDuration = 1f / TicksPerSecond;

        /// <summary>
        /// Сколько тиков максимум можно выполнить за один кадр. Защищает от лавины тиков после долгого
        /// кадра (сворачивание приложения, отладчик): лишнее время отбрасывается.
        /// </summary>
        public const int MaxTicksPerAdvance = 5;
    }
}
