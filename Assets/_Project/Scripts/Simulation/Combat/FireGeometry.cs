using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Combat
{
    /// <summary>
    /// Геометрия огня: линия фронта стрелка, сектор огня от флангов и дистанция от фронта до цели.
    /// </summary>
    public static class FireGeometry
    {
        /// <summary>Середина переднего края отряда.</summary>
        public static float2 FrontCenter(Squad squad, SquadSettings settings)
        {
            float2 half = FormationLayout.GetHalfExtents(squad.Formation, squad.Alive, settings);
            return squad.Position + Angles.Direction(squad.Facing) * half.y;
        }

        /// <summary>Половина ширины фронта.</summary>
        public static float FrontHalfWidth(Squad squad, SquadSettings settings)
        {
            return FormationLayout.GetHalfExtents(squad.Formation, squad.Alive, settings).x;
        }

        /// <summary>
        /// Лежит ли точка в секторе огня: впереди фронта и не дальше в сторону, чем
        /// halfWidth + расстояние вперёд × tan(arcHalfAngle).
        /// </summary>
        public static bool IsInSector(Squad shooter, float2 point, SimulationSettings settings)
        {
            float2 offset = point - FrontCenter(shooter, settings.squads);
            float forward = math.dot(offset, Angles.Direction(shooter.Facing));
            if (forward < 0f)
            {
                return false;
            }

            float lateral = math.abs(math.dot(offset, Angles.Right(shooter.Facing)));
            float spread = math.tan(math.radians(settings.combat.fireArcHalfAngle));
            return lateral <= FrontHalfWidth(shooter, settings.squads) + forward * spread;
        }

        /// <summary>Дистанция от линии фронта стрелка до ближайшей точки прямоугольника цели.</summary>
        public static float Distance(Squad shooter, Squad target, SquadSettings settings)
        {
            float2 frontCenter = FrontCenter(shooter, settings);
            float halfWidth = FrontHalfWidth(shooter, settings);
            float2 right = Angles.Right(shooter.Facing);
            float2 left = frontCenter - right * halfWidth;
            float2 rightEnd = frontCenter + right * halfWidth;

            SquadFootprint targetBox = SquadFootprint.Of(target, settings);
            var frontLine = new SquadFootprint(frontCenter, shooter.Facing, new float2(halfWidth, 0f));
            if (SquadFootprint.TryGetPenetration(frontLine, targetBox, out _))
            {
                return 0f;
            }

            // У двух непересекающихся выпуклых фигур ближайшая пара точек включает вершину одной из них.
            float distance = math.min(DistanceToBox(left, targetBox), DistanceToBox(rightEnd, targetBox));
            float2 h = targetBox.HalfExtents;
            for (int corner = 0; corner < 4; corner++)
            {
                float sx = (corner & 1) == 0 ? -1f : 1f;
                float sy = (corner & 2) == 0 ? -1f : 1f;
                float2 point = targetBox.Center + targetBox.Right * (h.x * sx) + targetBox.Forward * (h.y * sy);
                distance = math.min(distance, DistanceToSegment(point, left, rightEnd));
            }

            return distance;
        }

        private static float DistanceToBox(float2 point, SquadFootprint box)
        {
            float2 offset = point - box.Center;
            float2 local = new float2(math.dot(offset, box.Right), math.dot(offset, box.Forward));
            float2 outside = math.max(math.abs(local) - box.HalfExtents, 0f);
            return math.length(outside);
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
