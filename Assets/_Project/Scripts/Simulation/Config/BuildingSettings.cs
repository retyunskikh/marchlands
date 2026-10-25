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

        /// <summary>Цена найма отряда.</summary>
        public int squadCost;

        /// <summary>Время найма одного отряда, s.</summary>
        public float recruitTime;

        /// <summary>Сколько отрядов можно поставить в очередь казармы.</summary>
        public int recruitQueueMax;

        /// <summary>На каком расстоянии от казармы появляется нанятый отряд, u.</summary>
        public float spawnOffset;
    }
}
