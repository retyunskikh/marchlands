using System.Collections.Generic;
using Unity.Mathematics;

namespace Marchlands.Simulation.Terrain
{
    /// <summary>
    /// Плавная линия через опорные точки — сплайн Катмулла — Рома с шагом step. Проходит через все опорные точки
    /// (деревни и развилки остаются на дороге). Одна кривая и для симуляции (дороги и реки хранятся уже сглаженными),
    /// и для отрисовки: где дорога нарисована, там отряд по ней и идёт.
    /// </summary>
    public static class Spline
    {
        public static float2[] Smooth(IReadOnlyList<float2> points, float step)
        {
            if (points.Count < 2)
            {
                var copy = new float2[points.Count];
                for (int i = 0; i < copy.Length; i++)
                {
                    copy[i] = points[i];
                }

                return copy;
            }

            var result = new List<float2> { points[0] };
            for (int i = 0; i + 1 < points.Count; i++)
            {
                float2 p0 = points[math.max(0, i - 1)];
                float2 p1 = points[i];
                float2 p2 = points[i + 1];
                float2 p3 = points[math.min(points.Count - 1, i + 2)];
                int samples = math.max(1, (int)math.ceil(math.distance(p1, p2) / math.max(0.05f, step)));
                for (int s = 1; s <= samples; s++)
                {
                    float t = s / (float)samples;
                    float t2 = t * t;
                    float t3 = t2 * t;
                    result.Add(0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                        + (3f * p1 - p0 - 3f * p2 + p3) * t3));
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Прореживание ломаной (Дуглас — Пекер): выкидывает точки, без которых линия отходит от исходной не больше
        /// <paramref name="tolerance"/>. Концы остаются.
        /// </summary>
        public static void Simplify(List<float2> points, float tolerance)
        {
            if (points.Count < 3)
            {
                return;
            }

            var keep = new bool[points.Count];
            keep[0] = keep[points.Count - 1] = true;
            var stack = new Stack<int2>();
            stack.Push(new int2(0, points.Count - 1));
            while (stack.Count > 0)
            {
                int2 span = stack.Pop();
                float worst = tolerance;
                int index = -1;
                for (int i = span.x + 1; i < span.y; i++)
                {
                    float distance = TerrainMap.DistanceToSegment(points[i], points[span.x], points[span.y]);
                    if (distance > worst)
                    {
                        worst = distance;
                        index = i;
                    }
                }

                if (index < 0)
                {
                    continue;
                }

                keep[index] = true;
                stack.Push(new int2(span.x, index));
                stack.Push(new int2(index, span.y));
            }

            int write = 0;
            for (int i = 0; i < points.Count; i++)
            {
                if (keep[i])
                {
                    points[write++] = points[i];
                }
            }

            points.RemoveRange(write, points.Count - write);
        }
    }
}
