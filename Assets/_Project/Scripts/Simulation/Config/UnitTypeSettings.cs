using System;

namespace Marchlands.Simulation.Config
{
    /// <summary>Параметры рода войск (раздел 3.9 и 6 docs/design.md). Множители — к общим параметрам отряда и боя.</summary>
    [Serializable]
    public sealed class UnitTypeSettings
    {
        /// <summary>Численность нового отряда; столько людей нужно для его формирования.</summary>
        public int squadSize;

        /// <summary>Меньше этого — уже не отряд: бежит и уходит в резерв.</summary>
        public int minSquadSize;

        /// <summary>Множитель скорости марша.</summary>
        public float speedMultiplier;

        /// <summary>Множитель вероятности попадания.</summary>
        public float accuracyMultiplier;

        /// <summary>Множитель всех дальностей огня (в упор, эффективной, максимальной).</summary>
        public float rangeMultiplier;

        /// <summary>Множитель потери морали от потерь.</summary>
        public float moraleLossMultiplier;

        /// <summary>Множитель потерь, которые отряд наносит в рукопашной.</summary>
        public float meleeMultiplier;

        /// <summary>Может ли отряд стрелять залпом.</summary>
        public bool canFire;

        /// <summary>Стреляет ли вразнобой — каждый сам по себе, когда враг в эффективной дальности (толпа рекрутов).</summary>
        public bool skirmishFire;

        /// <summary>Во сколько раз больше обычного бонус атаки (сверх 1): 2 — бонус 1.5 становится 2.0.</summary>
        public float chargeBonusMultiplier;

        /// <summary>Множитель вероятности попадания по этому отряду.</summary>
        public float targetHitMultiplier;
    }
}
