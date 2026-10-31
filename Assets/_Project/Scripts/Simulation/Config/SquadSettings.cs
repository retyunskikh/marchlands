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
        /// <summary>Приказ движения игрока дальше этого прокладывается по местности (дороги, обход лесов и рек), u.</summary>
        public float autoRouteMinDistance;

        /// <summary>
        /// Маршрут (в колонне, с перестроением) берётся, если он быстрее прямого пути в нынешнем строю хотя бы
        /// в столько раз (0.9 — на 10%); иначе отряд идёт напрямую.
        /// </summary>
        public float autoRouteGain;

        /// <summary>Число шеренг в линии.</summary>
        public int lineRanks;

        /// <summary>Ширина фронта колонны (сколько солдат в первой шеренге).</summary>
        public int columnFrontWidth;

        /// <summary>Интервал между солдатами в шеренге, u.</summary>
        public float fileSpacing;

        /// <summary>Интервал между солдатами в шеренге колонны (плечом к плечу, чтобы уместиться на дороге), u; 0 — как fileSpacing.</summary>
        public float columnFileSpacing;

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

        /// <summary>Доля скорости прежнего строя, с которой отряд идёт к цели, пока перестраивается.</summary>
        public float reformMoveSpeedMultiplier;

        /// <summary>Расстояние до точки, при котором отряд считается дошедшим, u.</summary>
        public float arrivalTolerance;

        /// <summary>
        /// Точка позади и ближе этого — отряд не разворачивается, а пятится, сохраняя фронт (и встаёт тем же фронтом), u.
        /// </summary>
        public float backstepDistance;

        /// <summary>Во сколько раз медленнее отряд пятится.</summary>
        public float backstepSpeedMultiplier;

        /// <summary>
        /// Колонна на марше поворачивает на ходу и срезает промежуточные точки пути: ближе этого к точке — идёт
        /// к следующей. Так же бегущие идут по точкам пути бегства, u.
        /// </summary>
        public float columnWaypointReach;

        /// <summary>
        /// Маршрут по дороге идёт по её оси и изгибам, если так не дальше стольких длин прямого отрезка между точками
        /// пути; иначе (дорога петляет в сторону) точки пути остаются как есть.
        /// </summary>
        public float roadAlignMaxDetour;

        /// <summary>
        /// Путь, который не меньше roadRouteMinShare своей длины идёт по дорогам, берётся, даже если в том же строю
        /// он медленнее прямого — до roadRouteGain раз: по дороге в колонне всё равно быстрее, чем линией по полю.
        /// </summary>
        public float roadRouteGain;

        /// <summary>Какая доля длины пути должна идти по дорогам, чтобы действовал roadRouteGain.</summary>
        public float roadRouteMinShare;

        /// <summary>Отложенный марш (развернули на месте, рукопашная) продолжается, когда не бегущих врагов нет ближе этого, u.</summary>
        public float resumeMarchEnemyRadius;
    }
}
