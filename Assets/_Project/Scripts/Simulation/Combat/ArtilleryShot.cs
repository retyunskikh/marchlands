using Marchlands.Simulation.Core;
using Unity.Mathematics;

namespace Marchlands.Simulation.Combat
{
    /// <summary>
    /// Выстрел пушки за тик — для отображения: полёт ядра от ствола до точки падения и рикошет до конца пути
    /// или веер картечи к цели.
    /// </summary>
    public readonly struct ArtilleryShot
    {
        public ArtilleryShot(int tick, int shooterId, Side side, ShotMode mode, float2 muzzle, float2 impact, float2 ricochetEnd, int casualties, int targetId)
        {
            Tick = tick;
            ShooterId = shooterId;
            Side = side;
            Mode = mode;
            Muzzle = muzzle;
            Impact = impact;
            RicochetEnd = ricochetEnd;
            Casualties = casualties;
            TargetId = targetId;
        }

        public int Tick { get; }

        public int ShooterId { get; }

        public Side Side { get; }

        public ShotMode Mode { get; }

        /// <summary>Откуда вылетел заряд (дуло).</summary>
        public float2 Muzzle { get; }

        /// <summary>Куда упало ядро; у картечи — центр цели.</summary>
        public float2 Impact { get; }

        /// <summary>Докуда докатилось ядро рикошетом; у картечи совпадает с <see cref="Impact"/>.</summary>
        public float2 RicochetEnd { get; }

        /// <summary>Сколько убито всего (по всем задетым отрядам).</summary>
        public int Casualties { get; }

        /// <summary>По какому отряду стреляли; 0 — по зданию.</summary>
        public int TargetId { get; }
    }
}
