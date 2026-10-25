using Unity.Mathematics;

namespace Marchlands.Simulation.Core
{
    /// <summary>
    /// Углы в симуляции — в градусах, против часовой стрелки от оси +X. 0° — фронт смотрит вправо, 90° — вверх.
    /// </summary>
    public static class Angles
    {
        private const float FullTurn = 360f;
        private const float HalfTurn = 180f;

        /// <summary>Приводит угол к диапазону (-180, 180].</summary>
        public static float Normalize(float degrees)
        {
            float result = degrees % FullTurn;
            if (result <= -HalfTurn)
            {
                result += FullTurn;
            }
            else if (result > HalfTurn)
            {
                result -= FullTurn;
            }

            return result;
        }

        /// <summary>Кратчайший поворот от <paramref name="from"/> к <paramref name="to"/>, в (-180, 180].</summary>
        public static float Delta(float from, float to)
        {
            return Normalize(to - from);
        }

        public static float2 Direction(float degrees)
        {
            math.sincos(math.radians(degrees), out float sin, out float cos);
            return new float2(cos, sin);
        }

        /// <summary>Направление «вправо по фронту» для отряда, смотрящего под углом <paramref name="degrees"/>.</summary>
        public static float2 Right(float degrees)
        {
            float2 forward = Direction(degrees);
            return new float2(forward.y, -forward.x);
        }

        public static float Of(float2 direction)
        {
            return math.degrees(math.atan2(direction.y, direction.x));
        }
    }
}
