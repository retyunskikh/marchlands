using System.Collections.Generic;
using Marchlands.Simulation.Config;
using Unity.Mathematics;

namespace Marchlands.Simulation.Terrain
{
    /// <summary>
    /// Поиск пути ИИ с учётом местности: A* по сетке ячеек pathCellSize, стоимость — время прохода
    /// (расстояние / множитель скорости), граница непроходима. Найденный путь спрямляется: промежуточная точка
    /// выбрасывается, если прямой отрезок мимо неё не дольше. Рощи для пути шире на forestClearance: отряд,
    /// срезающий угол к следующей точке, остаётся в поле. Детерминирован: при равной оценке побеждает
    /// ячейка с меньшим индексом.
    /// </summary>
    public sealed class TerrainPathfinder
    {
        // Шаг проверки отрезка при спрямлении — доля размера ячейки.
        private const float SampleStepFraction = 0.25f;

        // Прямой отрезок не должен проигрывать ломаной из-за ошибки округления сумм.
        private const float ShortcutTolerance = 1.0001f;
        private const float Sqrt2 = 1.41421356f;

        private static readonly int2[] Neighbours =
        {
            new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1),
            new int2(1, 1), new int2(1, -1), new int2(-1, 1), new int2(-1, -1)
        };

        private readonly TerrainMap map;
        private readonly TerrainSettings settings;
        private readonly float cellSize;
        private readonly int columns;
        private readonly int rows;
        private readonly float[] cellCost;
        private readonly float minCost;
        private readonly float forestClearance;

        private readonly float[] gScore;
        private readonly int[] cameFrom;
        private readonly bool[] closed;
        private readonly SortedSet<(float f, int index)> open = new SortedSet<(float f, int index)>();
        private readonly List<float2> raw = new List<float2>();

        /// <param name="forestClearance">На сколько обходить опушку, u.</param>
        public TerrainPathfinder(TerrainMap map, TerrainSettings settings, float forestClearance = 0f)
        {
            this.map = map;
            this.settings = settings;
            this.forestClearance = forestClearance;
            cellSize = settings.pathCellSize;
            float2 size = map.BoundsMax - map.BoundsMin;
            columns = math.max(1, (int)math.ceil(size.x / cellSize));
            rows = math.max(1, (int)math.ceil(size.y / cellSize));

            int count = columns * rows;
            cellCost = new float[count];
            gScore = new float[count];
            cameFrom = new int[count];
            closed = new bool[count];

            minCost = float.MaxValue;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    float speed = PathSpeed(CellCenter(x, y));
                    float cost = speed > 0f ? 1f / speed : float.PositiveInfinity;
                    cellCost[y * columns + x] = cost;
                    minCost = math.min(minCost, cost);
                }
            }
        }

        /// <summary>
        /// Заполняет <paramref name="waypoints"/> точками пути от <paramref name="from"/> до <paramref name="to"/>
        /// (без начальной, последняя — сама цель). false — пути нет.
        /// </summary>
        public bool FindPath(float2 from, float2 to, List<float2> waypoints)
        {
            waypoints.Clear();
            to = map.Clamp(to, float2.zero);
            int start = CellIndex(map.Clamp(from, float2.zero));
            int goal = CellIndex(to);

            for (int i = 0; i < gScore.Length; i++)
            {
                gScore[i] = float.PositiveInfinity;
                cameFrom[i] = -1;
                closed[i] = false;
            }

            open.Clear();
            gScore[start] = 0f;
            open.Add((Heuristic(start, goal), start));

            while (open.Count > 0)
            {
                (float _, int current) = open.Min;
                open.Remove(open.Min);
                if (current == goal)
                {
                    BuildPath(from, to, goal, waypoints);
                    return true;
                }

                if (closed[current])
                {
                    continue;
                }

                closed[current] = true;
                int cx = current % columns;
                int cy = current / columns;
                foreach (int2 step in Neighbours)
                {
                    int nx = cx + step.x;
                    int ny = cy + step.y;
                    if (nx < 0 || ny < 0 || nx >= columns || ny >= rows)
                    {
                        continue;
                    }

                    int next = ny * columns + nx;
                    if (closed[next] || float.IsInfinity(cellCost[next]))
                    {
                        continue;
                    }

                    float length = step.x != 0 && step.y != 0 ? Sqrt2 : 1f;
                    float tentative = gScore[current] + length * cellSize * 0.5f * (cellCost[current] + cellCost[next]);
                    if (tentative >= gScore[next])
                    {
                        continue;
                    }

                    if (!float.IsInfinity(gScore[next]))
                    {
                        open.Remove((gScore[next] + Heuristic(next, goal), next));
                    }

                    gScore[next] = tentative;
                    cameFrom[next] = current;
                    open.Add((tentative + Heuristic(next, goal), next));
                }
            }

            return false;
        }

        /// <summary>Время прохода отрезка по местности (в «секундах при скорости 1»), с шагом SampleStepFraction ячейки.</summary>
        public float SegmentCost(float2 a, float2 b)
        {
            float length = math.distance(a, b);
            if (length <= 0f)
            {
                return 0f;
            }

            float step = cellSize * SampleStepFraction;
            int samples = math.max(1, (int)math.ceil(length / step));
            float cost = 0f;
            for (int i = 0; i < samples; i++)
            {
                float2 point = math.lerp(a, b, (i + 0.5f) / samples);
                float speed = PathSpeed(point);
                if (speed <= 0f)
                {
                    return float.PositiveInfinity;
                }

                cost += 1f / speed;
            }

            return cost * length / samples;
        }

        /// <summary>Скорость для поиска пути: как на местности, но рощи шире на forestClearance.</summary>
        private float PathSpeed(float2 point)
        {
            if (!map.Contains(point))
            {
                return 0f;
            }

            foreach (Forest forest in map.Forests)
            {
                float radius = forest.Radius + forestClearance;
                if (math.distancesq(point, forest.Center) <= radius * radius)
                {
                    return settings.forestSpeedMultiplier;
                }
            }

            return map.SpeedMultiplier(point, settings);
        }

        private void BuildPath(float2 from, float2 to, int goal, List<float2> waypoints)
        {
            raw.Clear();
            for (int cell = goal; cell != -1; cell = cameFrom[cell])
            {
                raw.Add(CellCenter(cell % columns, cell / columns));
            }

            raw.Reverse();
            // Первая ячейка — та, где стоит отряд: вместо её центра берём сам отряд, последнюю — точную цель.
            raw[0] = from;
            raw[raw.Count - 1] = to;

            // Жадное спрямление: из текущей точки тянемся к самой дальней, до которой прямо не дольше, чем по ломаной.
            int anchor = 0;
            while (anchor < raw.Count - 1)
            {
                int best = anchor + 1;
                float pathCost = 0f;
                for (int candidate = anchor + 1; candidate < raw.Count; candidate++)
                {
                    pathCost += SegmentCost(raw[candidate - 1], raw[candidate]);
                    if (SegmentCost(raw[anchor], raw[candidate]) <= pathCost * ShortcutTolerance)
                    {
                        best = candidate;
                    }
                }

                waypoints.Add(raw[best]);
                anchor = best;
            }
        }

        private float Heuristic(int cell, int goal)
        {
            int2 a = new int2(cell % columns, cell / columns);
            int2 b = new int2(goal % columns, goal / columns);
            return math.distance((float2)a, (float2)b) * cellSize * minCost;
        }

        private int CellIndex(float2 point)
        {
            float2 local = (point - map.BoundsMin) / cellSize;
            int x = math.clamp((int)math.floor(local.x), 0, columns - 1);
            int y = math.clamp((int)math.floor(local.y), 0, rows - 1);
            return y * columns + x;
        }

        private float2 CellCenter(int x, int y)
        {
            return map.BoundsMin + new float2(x + 0.5f, y + 0.5f) * cellSize;
        }
    }
}
