using System.Collections.Generic;
using Marchlands.Simulation.Core;
using Unity.Mathematics;

namespace Marchlands.Simulation.Combat
{
    /// <summary>
    /// Ядро в полёте: летит от ствола до точки падения FlightTime секунд, затем катится рикошетом до End со скоростью
    /// ricochetSpeed. Бьёт отряды и здания, когда до них долетает, а не в момент выстрела.
    /// </summary>
    internal sealed class RoundShotFlight
    {
        public int GunId;
        public Side Side;
        public float2 Muzzle;
        public float2 Impact;
        public float2 End;
        public float FlightTime;
        public float Age;

        /// <summary>Сколько ядро уже прокатилось от точки падения, u.</summary>
        public float Rolled;

        public bool Landed;

        /// <summary>Отряды, через которые ядро уже прошло (каждый — один раз).</summary>
        public readonly List<int> HitSquads = new List<int>();
    }
}
