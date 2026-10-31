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
    /// Местность полной карты (раздел 3.8 docs/design.md): прямоугольник проходимой зоны (за ним лес и реки),
    /// рощи-круги, дороги-ломаные, а на больших картах — река через поле с бродами и мостами и пологие холмы.
    /// Задаётся до начала матча и дальше не меняется.
    /// </summary>
    public sealed class TerrainMap
    {
        // Шаг проверки отрезка на воду, u: меньше полуширины любой реки.
        private const float WaterSampleStep = 0.5f;

        // Насколько точки берегов переправы отстоят от воды и насколько круг переправы шире реки, u.
        private const float BankOffset = 1.5f;
        private const float CrossingMargin = 2f;

        private readonly List<Forest> forests = new List<Forest>();
        private readonly List<float2[]> roads = new List<float2[]>();
        private readonly List<River> rivers = new List<River>();
        private readonly List<Crossing> crossings = new List<Crossing>();
        private readonly List<Hill> hills = new List<Hill>();

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

        public IReadOnlyList<River> Rivers => rivers;

        public IReadOnlyList<Crossing> Crossings => crossings;

        public IReadOnlyList<Hill> Hills => hills;

        public void AddRiver(IReadOnlyList<float2> points, float halfWidth)
        {
            var copy = new float2[points.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = points[i];
            }

            rivers.Add(new River(copy, halfWidth));
        }

        /// <summary>
        /// Переправа на ближайшей к <paramref name="point"/> реке: точка ставится на ось русла, берега — поперёк течения.
        /// Без рек переправа не добавляется.
        /// </summary>
        public void AddCrossing(float2 point, CrossingType type)
        {
            if (!TryNearestRiverPoint(point, out float2 center, out float2 along, out float halfWidth))
            {
                return;
            }

            // Рядом дорога пересекает реку — переправа ровно на пересечении и вдоль дороги: мост продолжает дорогу.
            if (TryRoadCrossing(point, out float2 at, out float2 roadDirection, out float2 riverDirection, out float riverHalfWidth))
            {
                float sine = math.max(0.35f, math.abs(roadDirection.x * riverDirection.y - roadDirection.y * riverDirection.x));
                float reach = riverHalfWidth / sine + BankOffset;
                crossings.Add(new Crossing(at, at - roadDirection * reach, at + roadDirection * reach,
                    riverHalfWidth / sine + CrossingMargin, type));
                return;
            }

            float2 across = new float2(-along.y, along.x);
            float bank = halfWidth + BankOffset;
            crossings.Add(new Crossing(center, center - across * bank, center + across * bank, halfWidth + CrossingMargin, type));
        }

        public void AddHill(float2 center, float radius)
        {
            hills.Add(new Hill(center, radius));
        }

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

        /// <summary>
        /// Множитель скорости в точке: лес, брод, дорога (и мост) или поле. За границей и в реке вне переправ — 0 (непроходимо).
        /// </summary>
        public float SpeedMultiplier(float2 point, TerrainSettings settings)
        {
            if (!Contains(point) || IsWater(point))
            {
                return 0f;
            }

            if (IsInForest(point))
            {
                return settings.forestSpeedMultiplier;
            }

            if (IsFord(point))
            {
                return settings.fordSpeedMultiplier;
            }

            return IsOnRoad(point, settings.roadHalfWidth) || IsOnBridge(point) ? settings.roadSpeedMultiplier : 1f;
        }

        /// <summary>Множитель попадания по отряду, центр которого в точке: в лесу — укрытие, на броде — открыт.</summary>
        public float TargetHitMultiplier(float2 point, TerrainSettings settings)
        {
            float multiplier = IsInForest(point) ? settings.forestCoverMultiplier : 1f;
            return IsFord(point) ? multiplier * settings.fordTargetMultiplier : multiplier;
        }

        /// <summary>Расстояние от точки до оси ближайшей реки, u; рек нет — бесконечность.</summary>
        public float DistanceToRivers(float2 point)
        {
            float best = float.PositiveInfinity;
            foreach (River river in rivers)
            {
                best = math.min(best, DistanceToPolyline(point, river.Points) - river.HalfWidth);
            }

            return best;
        }

        /// <summary>В реке и не на переправе.</summary>
        public bool IsWater(float2 point)
        {
            if (rivers.Count == 0 || CrossingAt(point) >= 0)
            {
                return false;
            }

            foreach (River river in rivers)
            {
                if (DistanceToPolyline(point, river.Points) < river.HalfWidth)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsFord(float2 point)
        {
            int index = CrossingAt(point);
            return index >= 0 && crossings[index].Type == CrossingType.Ford && IsInRiverBand(point);
        }

        /// <summary>
        /// Входит ли отряд с центром в <paramref name="point"/> на мост: центр в коридоре моста (не дальше
        /// <paramref name="lateral"/> от его оси вбок) и не дальше <paramref name="approach"/> от въезда за берегом.
        /// </summary>
        public bool IsEnteringBridge(float2 point, float lateral, float approach)
        {
            foreach (Crossing crossing in crossings)
            {
                if (crossing.Type != CrossingType.Bridge)
                {
                    continue;
                }

                float2 axis = crossing.Across;
                float length = math.distance(crossing.BankA, crossing.BankB);
                float2 offset = point - crossing.BankA;
                float along = math.dot(offset, axis);
                float side = math.abs(math.dot(offset, new float2(-axis.y, axis.x)));
                if (side <= lateral && along >= -approach && along <= length + approach)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsOnBridge(float2 point)
        {
            int index = CrossingAt(point);
            return index >= 0 && crossings[index].Type == CrossingType.Bridge && IsInRiverBand(point);
        }

        public bool IsOnHill(float2 point)
        {
            foreach (Hill hill in hills)
            {
                if (math.distancesq(point, hill.Center) <= hill.Radius * hill.Radius)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Идёт ли движение из точки по <paramref name="direction"/> вверх по склону какого-нибудь холма.</summary>
        public bool IsUphill(float2 point, float2 direction)
        {
            foreach (Hill hill in hills)
            {
                float2 toTop = hill.Center - point;
                if (math.lengthsq(toTop) <= hill.Radius * hill.Radius && math.dot(toTop, direction) > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Пересекает ли отрезок воду (реку вне переправ).</summary>
        public bool CrossesWater(float2 from, float2 to)
        {
            if (rivers.Count == 0)
            {
                return false;
            }

            float length = math.distance(from, to);
            int samples = math.max(1, (int)math.ceil(length / WaterSampleStep));
            for (int i = 0; i <= samples; i++)
            {
                if (IsWater(math.lerp(from, to, (float)i / samples)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Куда идти, чтобы попасть из <paramref name="from"/> в <paramref name="to"/>: напрямую, если по пути нет воды,
        /// иначе — к берегу самой выгодной переправы, по ней на другой берег и дальше к цели. Без состояния: решение
        /// принимается заново на каждом шаге.
        /// </summary>
        public float2 Steer(float2 from, float2 to)
        {
            if (crossings.Count == 0 || !CrossesWater(from, to))
            {
                return to;
            }

            // Уже на переправе — к тому берегу, откуда цель видна без воды.
            foreach (Crossing crossing in crossings)
            {
                if (math.distancesq(from, crossing.Center) > crossing.Radius * crossing.Radius)
                {
                    continue;
                }

                bool fromA = !CrossesWater(crossing.BankA, to);
                bool fromB = !CrossesWater(crossing.BankB, to);
                float2 exit = fromA && !fromB ? crossing.BankA
                    : fromB && !fromA ? crossing.BankB
                    : math.distancesq(crossing.BankA, to) < math.distancesq(crossing.BankB, to) ? crossing.BankA : crossing.BankB;
                return math.distancesq(from, exit) > 0.04f ? exit : to;
            }

            float bestCost = float.MaxValue;
            float2 best = to;
            foreach (Crossing crossing in crossings)
            {
                bool reachA = !CrossesWater(from, crossing.BankA);
                bool reachB = !CrossesWater(from, crossing.BankB);
                if (!reachA && !reachB)
                {
                    continue;
                }

                bool nearA = reachA && (!reachB || math.distancesq(from, crossing.BankA) <= math.distancesq(from, crossing.BankB));
                float2 near = nearA ? crossing.BankA : crossing.BankB;
                float2 far = nearA ? crossing.BankB : crossing.BankA;
                float cost = math.distance(from, near) + math.distance(near, far) + math.distance(far, to);
                if (CrossesWater(far, to))
                {
                    // С того берега до цели снова вода — переправа хуже, но лучше, чем стоять.
                    cost += 1000f;
                }

                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = near;
                }
            }

            return best;
        }

        /// <summary>
        /// Точка в реке (вне переправ) сдвигается на берег — на тот, что ближе к <paramref name="side"/>;
        /// точка на суше не меняется.
        /// </summary>
        public float2 OutOfWater(float2 point, float2 side)
        {
            if (!IsWater(point) || !TryNearestRiverPoint(point, out float2 axis, out float2 along, out float halfWidth))
            {
                return point;
            }

            float2 across = new float2(-along.y, along.x);
            float sign = math.dot(side - axis, across) >= 0f ? 1f : -1f;
            return axis + across * sign * (halfWidth + BankOffset);
        }

        private int CrossingAt(float2 point)
        {
            for (int i = 0; i < crossings.Count; i++)
            {
                if (math.distancesq(point, crossings[i].Center) <= crossings[i].Radius * crossings[i].Radius)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool IsInRiverBand(float2 point)
        {
            foreach (River river in rivers)
            {
                if (DistanceToPolyline(point, river.Points) < river.HalfWidth)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Ближайшее к точке (не дальше 10 u) пересечение дороги с рекой: где, направления дороги и реки, полуширина реки.</summary>
        private bool TryRoadCrossing(float2 point, out float2 at, out float2 roadDirection, out float2 riverDirection, out float halfWidth)
        {
            const float SearchRadius = 10f;
            at = point;
            roadDirection = riverDirection = float2.zero;
            halfWidth = 0f;
            float best = SearchRadius * SearchRadius;
            foreach (float2[] road in roads)
            {
                for (int i = 0; i + 1 < road.Length; i++)
                {
                    foreach (River river in rivers)
                    {
                        for (int j = 0; j + 1 < river.Points.Length; j++)
                        {
                            if (!TryIntersect(road[i], road[i + 1], river.Points[j], river.Points[j + 1], out float2 hit))
                            {
                                continue;
                            }

                            float distance = math.distancesq(hit, point);
                            if (distance < best)
                            {
                                best = distance;
                                at = hit;
                                roadDirection = math.normalize(road[i + 1] - road[i]);
                                riverDirection = math.normalize(river.Points[j + 1] - river.Points[j]);
                                halfWidth = river.HalfWidth;
                            }
                        }
                    }
                }
            }

            return math.lengthsq(roadDirection) > 0f;
        }

        private static bool TryIntersect(float2 a, float2 b, float2 c, float2 d, out float2 hit)
        {
            hit = float2.zero;
            float2 r = b - a;
            float2 s = d - c;
            float denominator = r.x * s.y - r.y * s.x;
            if (math.abs(denominator) < 1e-6f)
            {
                return false;
            }

            float2 ac = c - a;
            float t = (ac.x * s.y - ac.y * s.x) / denominator;
            float u = (ac.x * r.y - ac.y * r.x) / denominator;
            if (t < 0f || t > 1f || u < 0f || u > 1f)
            {
                return false;
            }

            hit = a + r * t;
            return true;
        }

        private bool TryNearestRiverPoint(float2 point, out float2 nearest, out float2 along, out float halfWidth)
        {
            nearest = point;
            along = new float2(0f, 1f);
            halfWidth = 0f;
            float best = float.MaxValue;
            foreach (River river in rivers)
            {
                for (int i = 0; i + 1 < river.Points.Length; i++)
                {
                    float2 a = river.Points[i];
                    float2 b = river.Points[i + 1];
                    float2 ab = b - a;
                    float lengthSq = math.lengthsq(ab);
                    float t = lengthSq > 0f ? math.saturate(math.dot(point - a, ab) / lengthSq) : 0f;
                    float2 onSegment = a + ab * t;
                    float distance = math.distancesq(point, onSegment);
                    if (distance < best)
                    {
                        best = distance;
                        nearest = onSegment;
                        along = math.normalizesafe(ab, along);
                        halfWidth = river.HalfWidth;
                    }
                }
            }

            return best < float.MaxValue;
        }

        private static float DistanceToPolyline(float2 point, float2[] points)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < points.Length; i++)
            {
                best = math.min(best, DistanceToSegment(point, points[i], points[i + 1]));
            }

            return best;
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
