using System;

namespace Marchlands.Simulation.Config
{
    /// <summary>
    /// Туман войны: вражеские отряды видны только в радиусе обзора своих отрядов, зданий и деревень.
    /// Воздушный шар, изученный в штабе, снимает туман для своей стороны до конца матча.
    /// </summary>
    [Serializable]
    public sealed class VisionSettings
    {
        /// <summary>Обзор пехотного отряда (и толпы рекрутов), u.</summary>
        public float infantryVisionRange;

        /// <summary>Обзор конного отряда, u.</summary>
        public float cavalryVisionRange;

        /// <summary>Обзор пушки с расчётом, u.</summary>
        public float artilleryVisionRange;

        /// <summary>Обзор своего построенного здания, u.</summary>
        public float buildingVisionRange;

        /// <summary>Обзор своей деревни (от её центра), u.</summary>
        public float villageVisionRange;

        /// <summary>Воздушный шар: цена изучения в штабе и сколько его изучают, s.</summary>
        public int balloonCost;

        public float balloonResearchTime;
    }
}
