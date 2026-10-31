using Unity.Mathematics;

namespace Marchlands.Simulation.Terrain
{
    /// <summary>Река через поле: ломаная по оси русла и полуширина воды. Вплавь непроходима.</summary>
    public readonly struct River
    {
        public River(float2[] points, float halfWidth)
        {
            Points = points;
            HalfWidth = halfWidth;
        }

        public float2[] Points { get; }

        public float HalfWidth { get; }
    }

    public enum CrossingType
    {
        /// <summary>Брод: идут медленнее, по стоящим в воде попадают лучше.</summary>
        Ford,

        /// <summary>Мост: как обычная дорога.</summary>
        Bridge
    }

    /// <summary>
    /// Переправа через реку: круг <see cref="Radius"/> вокруг <see cref="Center"/>, где вода проходима,
    /// и две точки на берегах по обе стороны, через которые идут к переправе и от неё.
    /// </summary>
    public readonly struct Crossing
    {
        public Crossing(float2 center, float2 bankA, float2 bankB, float radius, CrossingType type)
        {
            Center = center;
            BankA = bankA;
            BankB = bankB;
            Radius = radius;
            Type = type;
        }

        public float2 Center { get; }

        public float2 BankA { get; }

        public float2 BankB { get; }

        public float Radius { get; }

        public CrossingType Type { get; }

        /// <summary>Направление поперёк реки (от берега A к берегу B).</summary>
        public float2 Across => math.normalizesafe(BankB - BankA);
    }

    /// <summary>Пологий холм — круг: с вершины стреляют дальше, вверх по склону атакуют медленнее.</summary>
    public readonly struct Hill
    {
        public Hill(float2 center, float radius)
        {
            Center = center;
            Radius = radius;
        }

        public float2 Center { get; }

        public float Radius { get; }
    }
}
