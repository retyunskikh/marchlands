using System;

namespace Marchlands.Simulation.Config
{
    /// <summary>Параметры боевого духа (раздел 3.3 и 6 docs/design.md). Шкала морали — 0–100.</summary>
    [Serializable]
    public sealed class MoraleSettings
    {
        /// <summary>Во сколько раз доля потерь (в процентах) превращается в потерю морали.</summary>
        public float moraleShockFactor;

        public float pointBlankMoraleMultiplier;

        /// <summary>Множитель потери морали при огне или атаке во фланг и тыл.</summary>
        public float flankMoraleMultiplier;

        /// <summary>Полуугол фронтального сектора; стрелок или атакующий вне его — во фланге или тылу, °.</summary>
        public float frontalArcHalfAngle;

        /// <summary>Радиус, в котором бегство союзника бьёт по морали, u (от центра до центра).</summary>
        public float allyRoutRadius;

        public float allyRoutPenalty;

        /// <summary>Радиус, в котором бегство врага ободряет отряд, u (от центра до центра).</summary>
        public float enemyRoutRadius;

        /// <summary>Разовая прибавка морали, когда рядом обратился в бегство враг.</summary>
        public float enemyRoutBonus;

        /// <summary>Восстановление морали, в секунду.</summary>
        public float moraleRegen;

        /// <summary>Сколько секунд без потерь нужно для восстановления, s.</summary>
        public float moraleCalmTime;

        /// <summary>Радиус, в котором враг мешает восстановлению, u (от центра до центра).</summary>
        public float calmEnemyRadius;

        /// <summary>Ниже этого порога отряд бежит.</summary>
        public float routThreshold;

        /// <summary>Скорость бегства, u/s.</summary>
        public float routSpeed;

        /// <summary>Скорость разворота бегущего отряда к точке отхода, °/s; бежать он начинает, развернувшись.</summary>
        public float routRotationSpeed;
    }
}
