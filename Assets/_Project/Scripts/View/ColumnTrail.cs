using System.Collections.Generic;
using Marchlands.Simulation.Core;
using Unity.Mathematics;

namespace Marchlands.View
{
    /// <summary>
    /// След головы колонны на марше: ломаная, по которой прошла середина первой шеренги. Шеренги колонны
    /// расставляются вдоль неё — каждая там, где голова была на своём расстоянии назад, — и колонна изгибается
    /// по дороге, как настоящая, а не поворачивает прямоугольником. Только отображение.
    /// </summary>
    internal sealed class ColumnTrail
    {
        // Голова сдвинулась на столько — новая точка следа (чаще не нужно: изгиб и так плавный), u.
        private const float PointStep = 0.2f;

        private readonly List<float2> points = new List<float2>();
        private float2 head;
        private bool valid;

        /// <summary>
        /// Новое положение головы. Не дальше <paramref name="keepLength"/> назад след хранится; дальше — обрезается.
        /// </summary>
        public void Advance(float2 newHead, float keepLength)
        {
            if (!valid)
            {
                return;
            }

            head = newHead;
            float2 last = points[points.Count - 1];
            if (math.distance(head, last) >= PointStep)
            {
                points.Add(head);
            }

            Trim(keepLength);
        }

        /// <summary>След — прямая назад от головы по фронту <paramref name="facing"/>: строй как прямоугольник.</summary>
        public void Straighten(float2 newHead, float facing, float length)
        {
            head = newHead;
            points.Clear();
            points.Add(newHead - Angles.Direction(facing) * math.max(length, PointStep));
            points.Add(newHead);
            valid = true;
        }

        /// <summary>
        /// Точка следа на <paramref name="back"/> позади головы и направление движения в ней. Дальше конца следа —
        /// продолжение последнего отрезка по прямой.
        /// </summary>
        public void Sample(float back, out float2 point, out float2 direction)
        {
            float2 newer = head;
            for (int i = points.Count - 1; i >= 0; i--)
            {
                float2 older = points[i];
                float2 segment = newer - older;
                float length = math.length(segment);
                if (length <= 1e-5f)
                {
                    continue;
                }

                direction = segment / length;
                if (back <= length || i == 0)
                {
                    point = newer - direction * back;
                    return;
                }

                back -= length;
                newer = older;
            }

            direction = new float2(0f, 1f);
            point = head - direction * back;
        }

        private void Trim(float keepLength)
        {
            float total = math.distance(head, points[points.Count - 1]);
            for (int i = points.Count - 1; i > 0; i--)
            {
                total += math.distance(points[i], points[i - 1]);
                if (total > keepLength && i - 1 > 0)
                {
                    points.RemoveRange(0, i - 1);
                    return;
                }
            }
        }
    }
}
