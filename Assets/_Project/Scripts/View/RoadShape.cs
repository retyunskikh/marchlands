using System.Collections.Generic;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// Плавная линия дороги для отрисовки: сплайн Катмулла — Рома через точки ломаной, с шагом step.
    /// Проходит через все точки (деревни и развилки остаются на дороге). Ею же рисуются планы деревень,
    /// поэтому улицы в деревне совпадают с дорогой. Движение в симуляции по-прежнему считается по ломаной.
    /// </summary>
    public static class RoadShape
    {
        public static List<Vector2> Smooth(IReadOnlyList<Vector2> points, float step)
        {
            var result = new List<Vector2>();
            if (points.Count < 2)
            {
                result.AddRange(points);
                return result;
            }

            result.Add(points[0]);
            for (int i = 0; i + 1 < points.Count; i++)
            {
                Vector2 p0 = points[Mathf.Max(0, i - 1)];
                Vector2 p1 = points[i];
                Vector2 p2 = points[i + 1];
                Vector2 p3 = points[Mathf.Min(points.Count - 1, i + 2)];
                int samples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(p1, p2) / Mathf.Max(0.05f, step)));
                for (int s = 1; s <= samples; s++)
                {
                    float t = s / (float)samples;
                    float t2 = t * t;
                    float t3 = t2 * t;
                    result.Add(0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3));
                }
            }

            return result;
        }
    }
}
