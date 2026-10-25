using System.Collections.Generic;
using Marchlands.Simulation.Config;
using Unity.Mathematics;

namespace Marchlands.Simulation.Terrain
{
    /// <summary>Роща — круг на карте.</summary>
    public readonly struct Forest
    {
        public Forest(float2 center, float radius)
        {
            Center = center;
            Radius = radius;
        }

        public float2 Center { get; }

        public float Radius { get; }
    }

    /// <summary>
    /// Местность полной карты (раздел 3.8 docs/design.md): прямоугольник проходимой зоны (за ним горы и реки),
    /// рощи-круги и дороги-ломаные. Задаётся до начала матча и дальше не меняется.
    /// </summary>
    public sealed class TerrainMap
    {
        private readonly List<Forest> forests = new List<Forest>();
        private readonly List<float2[]> roads = new List<float2[]>();

        public TerrainMap(float2 boundsMin, float2 boundsMax)
        {
            BoundsMin = math.min(boundsMin, boundsMax);
            BoundsMax = math.max(boundsMin, boundsMax);
        }

        /// <summary>Нижний левый угол проходимой зоны.</summary>
        public float2 BoundsMin { get; }

        /// <summary>Верхний правый угол проходимой зоны.</summary>
        public float2 BoundsMax { get; }

        public IReadOnlyList<Forest> Forests => forests;

        public IReadOnlyList<float2[]> Roads => roads;

        public void AddForest(float2 center, float radius)
        {
            forests.Add(new Forest(center, radius));
        }

        public void AddRoad(IReadOnlyList<float2> points)
        {
            var copy = new float2[points.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = points[i];
            }

            roads.Add(copy);
        }

        public bool Contains(float2 point)
        {
            return math.all(point >= BoundsMin) && math.all(point <= BoundsMax);
        }

        public bool IsInForest(float2 point)
        {
            foreach (Forest forest in forests)
            {
                if (math.distancesq(point, forest.Center) <= forest.Radius * forest.Radius)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsOnRoad(float2 point, float halfWidth)
        {
            foreach (float2[] road in roads)
            {
                for (int i = 0; i + 1 < road.Length; i++)
                {
                    if (DistanceToSegment(point, road[i], road[i + 1]) <= halfWidth)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Множитель скорости в точке: лес, дорога или поле. За границей — 0 (непроходимо).</summary>
        public float SpeedMultiplier(float2 point, TerrainSettings settings)
        {
            if (!Contains(point))
            {
                return 0f;
            }

            if (IsInForest(point))
            {
                return settings.forestSpeedMultiplier;
            }

            return IsOnRoad(point, settings.roadHalfWidth) ? settings.roadSpeedMultiplier : 1f;
        }

        /// <summary>Прижимает точку к проходимой зоне, оставляя от края запас <paramref name="margin"/> по каждой оси.</summary>
        public float2 Clamp(float2 point, float2 margin)
        {
            float2 min = BoundsMin + margin;
            float2 max = BoundsMax - margin;
            // Если отряд шире зоны, ставим его по центру.
            float2 center = (BoundsMin + BoundsMax) * 0.5f;
            min = math.min(min, center);
            max = math.max(max, center);
            return math.clamp(point, min, max);
        }

        private static float DistanceToSegment(float2 point, float2 a, float2 b)
        {
            float2 ab = b - a;
            float lengthSq = math.lengthsq(ab);
            float t = lengthSq > 0f ? math.saturate(math.dot(point - a, ab) / lengthSq) : 0f;
            return math.distance(point, a + ab * t);
        }
    }
}
