using System;

namespace Marchlands.Simulation.Config
{
    /// <summary>
    /// Туман войны: всё, что дальше радиуса обзора от своих отрядов, зданий (и строящихся) и деревень, закрыто.
    /// Радиус обзора — дальность выстрела гренадёров × visionRangeMultiplier, одинаковый для всех.
    /// Воздушный шар, изученный в штабе, снимает туман для своей стороны до конца матча.
    /// </summary>
    [Serializable]
    public sealed class VisionSettings
    {
        /// <summary>Во сколько раз обзор дальше выстрела гренадёров (combat.maxRange × grenadiers.rangeMultiplier).</summary>
        public float visionRangeMultiplier;

        /// <summary>Воздушный шар: цена изучения в штабе и сколько его изучают, s.</summary>
        public int balloonCost;

        public float balloonResearchTime;
    }
}
