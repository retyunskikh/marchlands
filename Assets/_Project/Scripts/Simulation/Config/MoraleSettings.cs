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

        /// <summary>Восстановление морали в спокойствии, в секунду.</summary>
        public float moraleRegen;

        /// <summary>Медленное восстановление всегда, даже в бою (кроме бегущих), в секунду.</summary>
        public float moraleBaseRegen;

        /// <summary>Прибавка морали стрелку или бойцу за каждого убитого врага.</summary>
        public float killMoraleGain;

        /// <summary>Прибавка морали за каждого человека, пришедшего пополнением из деревни.</summary>
        public float reinforceMoraleGain;

        /// <summary>Сколько секунд без потерь нужно для восстановления, s.</summary>
        public float moraleCalmTime;

        /// <summary>Радиус, в котором враг мешает восстановлению, u (от центра до центра).</summary>
        public float calmEnemyRadius;

        /// <summary>Ниже этого порога отряд бежит.</summary>
        public float routThreshold;

        /// <summary>Скорость бегства, u/s.</summary>
        public float routSpeed;

        /// <summary>Радиус «психологического давления»: враги и свои ближе этого считаются, u (от центра до центра).</summary>
        public float pressureRadius;

        /// <summary>Давление начинается, когда людей у наступающего врага рядом больше, чем у нас (с союзниками), в столько раз.</summary>
        public float pressureRatio;

        /// <summary>Потеря морали от давления в секунду на каждую единицу перевеса сверх порога (перевес / порог − 1).</summary>
        public float pressureMoraleLoss;

        /// <summary>Предел потери морали от давления, в секунду.</summary>
        public float pressureMaxLoss;

        /// <summary>Ниже этого давление само по себе дух не опускает (сломить отряд может только огонь и штык).</summary>
        public float pressureMoraleFloor;

        /// <summary>Наибольшая прибавка к пределу морали опытного отряда (сверх 100).</summary>
        public float veteranMaxMoraleBonus;

        /// <summary>
        /// Опыт: прибавка к пределу = veteranMaxMoraleBonus × (1 − e^(−убито / veteranKillScale)) — первые убитые
        /// поднимают предел заметно, дальше всё медленнее.
        /// </summary>
        public float veteranKillScale;

        /// <summary>Сколько убитых врагов нужно для знаков: бронзовая звезда, серебряная звезда, орден.</summary>
        public float veteranRank1Kills;

        public float veteranRank2Kills;

        public float veteranRank3Kills;

        /// <summary>Скорость разворота бегущего отряда к точке отхода, °/s; бежать он начинает, развернувшись.</summary>
        public float routRotationSpeed;
    }
}
