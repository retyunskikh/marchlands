using System;

namespace Marchlands.Simulation.Config
{
    /// <summary>
    /// Параметры пушек (раздел 3.10 docs/design.md): передок, перезарядка, ядро по навесной траектории с рикошетом
    /// и картечь. Значения задаются в конфиге баланса, здесь умолчаний нет намеренно.
    /// </summary>
    [Serializable]
    public sealed class GunSettings
    {
        /// <summary>Сколько секунд пушку снимают с передка (перед стрельбой) или берут на передок (перед маршем), s.</summary>
        public float limberTime;

        /// <summary>Перезарядка пушки, s.</summary>
        public float reloadTime;

        /// <summary>Наводка перед выстрелом, s.</summary>
        public float aimTime;

        /// <summary>Пушка бьёт в секторе ±fireArcHalfAngle от направления ствола, °.</summary>
        public float fireArcHalfAngle;

        /// <summary>Ядром — не ближе этого (навесная траектория), u.</summary>
        public float roundShotMinRange;

        /// <summary>Ядром — не дальше этого, u.</summary>
        public float roundShotRange;

        /// <summary>Разброс падения ядра вдоль выстрела: ± эта доля дистанции.</summary>
        public float roundShotSpread;

        /// <summary>Разброс падения ядра вбок: ± эта доля дистанции.</summary>
        public float roundShotLateralSpread;

        /// <summary>Упав, ядро рикошетом катится по прямой ещё столько, сметая всех на пути, u.</summary>
        public float ricochetLength;

        /// <summary>Вероятность, что ядро убьёт (выведет из строя) человека на своём пути.</summary>
        public float roundShotKillChance;

        /// <summary>Сколько здоровья снимает ядро, упавшее не дальше roundShotBuildingRadius от центра здания.</summary>
        public float roundShotBuildingDamage;

        public float roundShotBuildingRadius;

        /// <summary>Картечью — не дальше этого (от ствола до края строя цели), u.</summary>
        public float canisterRange;

        /// <summary>Сколько пуль в картечном заряде.</summary>
        public int canisterBalls;

        /// <summary>Вероятность попадания одной пули вплотную; к canisterRange падает до canisterHitFar.</summary>
        public float canisterHitNear;

        public float canisterHitFar;
    }
}
