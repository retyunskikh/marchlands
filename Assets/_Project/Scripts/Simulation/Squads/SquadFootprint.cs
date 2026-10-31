using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Unity.Mathematics;

namespace Marchlands.Simulation.Squads
{
    /// <summary>Прямоугольник, который занимает отряд на земле: центр, оси фронта и половины габаритов.</summary>
    public readonly struct SquadFootprint
    {
        private const int SeparatingAxisCount = 4;

        public SquadFootprint(float2 center, float facing, float2 halfExtents)
        {
            Center = center;
            Forward = Angles.Direction(facing);
            Right = Angles.Right(facing);
            HalfExtents = halfExtents;
        }

        public float2 Center { get; }

        public float2 Forward { get; }

        public float2 Right { get; }

        /// <summary>x — половина ширины фронта, y — половина глубины.</summary>
        public float2 HalfExtents { get; }

        /// <summary>Тот же прямоугольник, расширенный на <paramref name="margin"/> с каждой стороны.</summary>
        public SquadFootprint Inflated(float margin)
        {
            return new SquadFootprint(Center, Forward, Right, HalfExtents + margin);
        }

        private SquadFootprint(float2 center, float2 forward, float2 right, float2 halfExtents)
        {
            Center = center;
            Forward = forward;
            Right = right;
            HalfExtents = halfExtents;
        }

        /// <summary>Насколько два прямоугольника перекрываются (глубина вдоль разделяющей оси); 0 — не перекрываются.</summary>
        public static float PenetrationDepth(SquadFootprint a, SquadFootprint b)
        {
            return TryGetPenetration(a, b, out float2 push) ? math.length(push) : 0f;
        }

        public static SquadFootprint Of(Squad squad, SquadSettings settings)
        {
            return new SquadFootprint(squad.Position, squad.Facing, FormationLayout.GetHalfExtents(squad, settings));
        }

        /// <summary>
        /// Попадает ли точка в прямоугольник; каждая половина габарита считается не меньше
        /// <paramref name="minHalfExtent"/> — так узкую линию легко задеть пальцем.
        /// </summary>
        public bool Contains(float2 point, float minHalfExtent)
        {
            float2 offset = point - Center;
            float2 half = math.max(HalfExtents, minHalfExtent);
            return math.abs(math.dot(offset, Right)) <= half.x
                && math.abs(math.dot(offset, Forward)) <= half.y;
        }

        /// <summary>
        /// Если прямоугольники пересекаются, возвращает вектор, на который нужно сдвинуть <paramref name="b"/>
        /// относительно <paramref name="a"/>, чтобы они только касались (теорема о разделяющей оси).
        /// </summary>
        public static bool TryGetPenetration(SquadFootprint a, SquadFootprint b, out float2 push)
        {
            push = float2.zero;
            float2 offset = b.Center - a.Center;
            float minOverlap = float.MaxValue;
            float2 minAxis = float2.zero;

            for (int axisIndex = 0; axisIndex < SeparatingAxisCount; axisIndex++)
            {
                float2 axis = axisIndex switch
                {
                    0 => a.Right,
                    1 => a.Forward,
                    2 => b.Right,
                    _ => b.Forward
                };

                float distance = math.dot(offset, axis);
                float overlap = a.ProjectedRadius(axis) + b.ProjectedRadius(axis) - math.abs(distance);
                if (overlap <= 0f)
                {
                    return false;
                }

                if (overlap < minOverlap)
                {
                    minOverlap = overlap;
                    minAxis = distance < 0f ? -axis : axis;
                }
            }

            push = minAxis * minOverlap;
            return true;
        }

        private float ProjectedRadius(float2 axis)
        {
            return HalfExtents.x * math.abs(math.dot(Right, axis)) + HalfExtents.y * math.abs(math.dot(Forward, axis));
        }
    }
}
