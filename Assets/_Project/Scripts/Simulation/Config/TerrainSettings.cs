using System;

namespace Marchlands.Simulation.Config
{
    /// <summary>Параметры местности (раздел 3.8 и 6 docs/design.md).</summary>
    [Serializable]
    public sealed class TerrainSettings
    {
        /// <summary>Во сколько раз быстрее идёт отряд, центр которого на дороге.</summary>
        public float roadSpeedMultiplier;

        /// <summary>Полуширина дороги: центр отряда ближе этого к оси дороги — отряд на дороге, u.</summary>
        public float roadHalfWidth;

        /// <summary>Множитель скорости в лесу.</summary>
        public float forestSpeedMultiplier;

        /// <summary>Множитель вероятности попадания по отряду, центр которого в лесу.</summary>
        public float forestCoverMultiplier;

        /// <summary>Размер ячейки сетки поиска пути ИИ, u.</summary>
        public float pathCellSize;

        /// <summary>
        /// Насколько поиск пути предпочитает дороги сверх их скорости: время по дороге для него делится ещё и на это
        /// число. Так путь идёт по дороге, даже если она петляет, — по ней ходят колонной, а не линией по полю.
        /// </summary>
        public float pathRoadPreference;

        /// <summary>Множитель скорости на броде.</summary>
        public float fordSpeedMultiplier;

        /// <summary>Множитель вероятности попадания по отряду, центр которого на броде (в воде, на виду).</summary>
        public float fordTargetMultiplier;

        /// <summary>Множитель всех дальностей огня отряда, центр которого на холме.</summary>
        public float hillRangeMultiplier;

        /// <summary>Множитель скорости атаки (в штыки, в сабли, на здание) вверх по склону холма.</summary>
        public float uphillChargeMultiplier;

        /// <summary>
        /// Полуширина настила моста: на мосту строй сжимается до стольких рядов в ширину, сколько помещается
        /// (2 × bridgeHalfWidth / fileSpacing), и становится глубже, u.
        /// </summary>
        public float bridgeHalfWidth;
    }
}
