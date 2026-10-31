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

        /// <summary>Сколько пушек держит ИИ (0 — артдепо не строит).</summary>
        public int artilleryGuns;

        /// <summary>С какого расстояния пушка ИИ бьёт ядром: подходит к цели на это расстояние и встаёт, u.</summary>
        public float artilleryRange;

        /// <summary>ИИ закладывает дом, когда свободных мест остаётся меньше этого.</summary>
        public int houseMargin;

        /// <summary>Конница ИИ бросается в атаку на врага ближе этой дистанции (от фронта), u.</summary>
        public float cavalryChargeDistance;

        /// <summary>
        /// Конница ИИ не атакует в лоб: заходит во фланг или тыл цели — в точку на таком расстоянии сбоку
        /// от её центра — и бросается в атаку, оказавшись вне её фронта (или если цель уже в рукопашной), u.
        /// </summary>
        public float cavalryFlankDistance;

        /// <summary>
        /// Ничья или вражеская деревня ближе этого к отряду, а врагов рядом нет — отряд сначала захватывает её,
        /// даже если шёл к другой цели, u.
        /// </summary>
        public float opportunityVillageDistance;

        /// <summary>
        /// Выйдя на дистанцию огня, ИИ медлит со залпом случайное время в этих пределах (через GameRandom) —
        /// момент его залпа нельзя предсказать по секундам, s.
        /// </summary>
        public float volleyDelayMin;

        public float volleyDelayMax;
    }
}
