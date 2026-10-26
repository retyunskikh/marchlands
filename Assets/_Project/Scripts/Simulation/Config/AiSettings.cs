using System;

namespace Marchlands.Simulation.Config
{
    /// <summary>Параметры тактического ИИ (раздел 6 и этап 4 docs/design.md).</summary>
    [Serializable]
    public sealed class AiSettings
    {
        /// <summary>Через сколько секунд исполняется приказ ИИ — параметр сложности, s.</summary>
        public float reactionDelay;

        /// <summary>Как часто ИИ принимает решения, s.</summary>
        public float thinkInterval;

        /// <summary>ИИ идёт в штыки на перезаряжающегося врага ближе этой дистанции (от фронта), u.</summary>
        public float chargeDistance;

        /// <summary>…если его собственная мораль выше этого порога.</summary>
        public float chargeMinMorale;

        /// <summary>Ниже этого порога ИИ отводит отряд к точке отхода.</summary>
        public float retreatMorale;

        /// <summary>С этого порога отошедший отряд возвращается в бой.</summary>
        public float returnMorale;

        /// <summary>Насколько должна сместиться точка подхода, чтобы ИИ обновил приказ движения, u.</summary>
        public float repathDistance;

        /// <summary>Ближе этого (от центра до центра) ИИ вступает в бой; иначе отряд свободен для захвата деревень, u.</summary>
        public float engageRadius;

        /// <summary>Враг ближе этого к своему штабу или деревне — угроза, на которую идёт свободный отряд, u.</summary>
        public float defenseRadius;

        /// <summary>Ближе этого к цели ИИ идёт напрямую, дальше — по пути с учётом местности, u.</summary>
        public float directApproachDistance;

        /// <summary>Сколько отрядов конницы держит ИИ.</summary>
        public int cavalrySquads;

        /// <summary>ИИ закладывает дом, когда свободных мест остаётся меньше этого.</summary>
        public int houseMargin;

        /// <summary>Конница ИИ бросается в атаку на врага ближе этой дистанции (от фронта), u.</summary>
        public float cavalryChargeDistance;
    }
}
