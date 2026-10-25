using System;

namespace Marchlands.Simulation.Config
{
    /// <summary>Параметры зданий и найма (раздел 3.6 и 6 docs/design.md).</summary>
    [Serializable]
    public sealed class BuildingSettings
    {
        public int barracksCost;

        /// <summary>Время постройки казармы, s.</summary>
        public float barracksBuildTime;

        /// <summary>Казарму можно ставить не дальше этого от своего штаба, u.</summary>
        public float buildRadiusFromHQ;

        /// <summary>…и не ближе этого к другим зданиям и деревням, u.</summary>
        public float buildingSpacing;

        /// <summary>Цена одного рекрута.</summary>
        public int hireCost;

        /// <summary>Раз в столько секунд казарма с включённым наймом получает рекрута, s.</summary>
        public float hireInterval;

        /// <summary>Пока у казарм ждут столько людей, найм приостановлен.</summary>
        public int maxWaitingRecruits;

        public int stableCost;

        /// <summary>Время постройки конюшни, s.</summary>
        public float stableBuildTime;

        /// <summary>Цена одного всадника.</summary>
        public int cavalryHireCost;

        /// <summary>Раз в столько секунд конюшня с включённым наймом получает всадника, s.</summary>
        public float cavalryHireInterval;

        /// <summary>Пока у конюшен ждут столько всадников, найм приостановлен.</summary>
        public int maxWaitingCavalry;

        /// <summary>На каком расстоянии от казармы появляется нанятый отряд, u.</summary>
        public float spawnOffset;
    }
}
