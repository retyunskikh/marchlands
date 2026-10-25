using System;

namespace Marchlands.Simulation.Config
{
    /// <summary>Параметры штыковой атаки и рукопашной (раздел 3.4 и 6 docs/design.md).</summary>
    [Serializable]
    public sealed class MeleeSettings
    {
        /// <summary>Скорость бега в штыковую, u/s.</summary>
        public float chargeSpeed;

        /// <summary>Дистанция от фронта атакующего, на которой проверяется мораль защитника, u.</summary>
        public float chargeCheckDistance;

        /// <summary>Защитник с моралью ниже этого порога бежит, не принимая боя.</summary>
        public float chargeMoraleThreshold;

        /// <summary>Доля живых врага, которых каждый солдат стороны убивает в секунду.</summary>
        public float meleeKillRate;

        public float chargeBonus;

        /// <summary>Сколько секунд после касания действует бонус атакующего, s.</summary>
        public float chargeBonusDuration;

        public float flankMeleeMultiplier;

        /// <summary>Если за это время атака не дошла до контакта (путь перекрыт), отряд останавливается, s.</summary>
        public float chargeMaxDuration;
    }
}
