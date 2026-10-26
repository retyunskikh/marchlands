using System;

namespace Marchlands.Simulation.Config
{
    /// <summary>Параметры стрельбы (раздел 3.2 и 6 docs/design.md). Умолчаний нет — значения задаёт конфиг.</summary>
    [Serializable]
    public sealed class CombatSettings
    {
        /// <summary>Максимальная дальность залпа, u.</summary>
        public float maxRange;

        /// <summary>Эффективная дальность: ИИ держит огонь до неё, рекруты стреляют с неё, u.</summary>
        public float effectiveRange;

        /// <summary>Дистанция «в упор», u.</summary>
        public float pointBlankRange;

        /// <summary>Вероятность попадания одного стрелка на maxRange.</summary>
        public float hitMax;

        /// <summary>Вероятность попадания на effectiveRange.</summary>
        public float hitEffective;

        /// <summary>Вероятность попадания в упор.</summary>
        public float hitPointBlank;

        public float firstVolleyMultiplier;

        /// <summary>Множитель попадания по цели в колонне.</summary>
        public float columnTargetMultiplier;

        /// <summary>Полуугол сектора огня от флангов фронта, °.</summary>
        public float fireArcHalfAngle;

        /// <summary>Время прицеливания, s.</summary>
        public float aimTime;

        /// <summary>Время перезарядки, s.</summary>
        public float reloadTime;

        /// <summary>Множитель скорости марша во время перезарядки.</summary>
        public float reloadMoveSpeedMultiplier;
    }
}
