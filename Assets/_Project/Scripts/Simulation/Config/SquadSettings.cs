using System;

namespace Marchlands.Simulation.Config
{
    /// <summary>
    /// Параметры строя и движения отряда. Значения задаются в конфиге баланса (раздел 6 docs/design.md),
    /// здесь умолчаний нет намеренно.
    /// </summary>
    [Serializable]
    public sealed class SquadSettings
    {
        /// <summary>Численность нового отряда.</summary>
        public int squadSize;

        /// <summary>Число шеренг в линии.</summary>
        public int lineRanks;

        /// <summary>Ширина фронта колонны (сколько солдат в первой шеренге).</summary>
        public int columnFrontWidth;

        /// <summary>Интервал между солдатами в шеренге, u.</summary>
        public float fileSpacing;

        /// <summary>Дистанция между шеренгами, u.</summary>
        public float rankSpacing;

        /// <summary>Скорость марша в линии, u/s.</summary>
        public float lineSpeed;

        /// <summary>Скорость марша в колонне, u/s.</summary>
        public float columnSpeed;

        /// <summary>Скорость поворота, °/s.</summary>
        public float rotationSpeed;

        /// <summary>Время перестроения, s.</summary>
        public float formationChangeTime;

        /// <summary>Расстояние до точки, при котором отряд считается дошедшим, u.</summary>
        public float arrivalTolerance;
    }
}
