using System.Collections.Generic;
using Marchlands.Simulation.Terrain;
using Unity.Mathematics;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// Плавная линия дороги (и реки) для отрисовки — та же кривая, что в симуляции (<see cref="Spline"/>),
    /// с шагом step. Ею же рисуются планы деревень, поэтому улицы в деревне совпадают с дорогой.
    /// </summary>
    public static class RoadShape
    {
        public static List<Vector2> Smooth(IReadOnlyList<Vector2> points, float step)
        {
            var source = new float2[points.Count];
            for (int i = 0; i < source.Length; i++)
            {
                source[i] = points[i];
            }

            float2[] line = Spline.Smooth(source, step);
            var result = new List<Vector2>(line.Length);
            foreach (float2 point in line)
            {
                result.Add(new Vector2(point.x, point.y));
            }

            return result;
        }
    }
}
