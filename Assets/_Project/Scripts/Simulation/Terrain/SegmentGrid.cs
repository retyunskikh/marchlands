using System.Collections.Generic;
using Unity.Mathematics;

namespace Marchlands.Simulation.Terrain
{
    /// <summary>
    /// Отрезки ломаных (дорог или рек), разложенные по клеткам: в клетке — все отрезки, проходящие от неё ближе
    /// <see cref="Margin"/>. Проверка «ближе r к линии» при r ≤ Margin смотрит только отрезки своей клетки.
    /// </summary>
    internal sealed class SegmentGrid
    {
        private readonly float2 origin;
        private readonly float cellSize;
        private readonly int columns;
        private readonly int rows;
        private readonly List<int2>[] cells;

        public SegmentGrid(IReadOnlyList<float2[]> lines, float2 min, float2 max, float cellSize, float margin)
        {
            Margin = margin;
            this.cellSize = cellSize;
            origin = min - margin;
            float2 size = max - min + margin * 2f;
            columns = math.max(1, (int)math.ceil(size.x / cellSize));
            rows = math.max(1, (int)math.ceil(size.y / cellSize));
            cells = new List<int2>[columns * rows];
            for (int l = 0; l < lines.Count; l++)
            {
                float2[] line = lines[l];
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    float2 low = math.min(line[i], line[i + 1]) - margin;
                    float2 high = math.max(line[i], line[i + 1]) + margin;
                    int2 from = math.max((int2)math.floor((low - origin) / cellSize), int2.zero);
                    int2 to = math.min((int2)math.floor((high - origin) / cellSize), new int2(columns - 1, rows - 1));
                    for (int y = from.y; y <= to.y; y++)
                    {
                        for (int x = from.x; x <= to.x; x++)
                        {
                            int index = y * columns + x;
                            cells[index] ??= new List<int2>();
                            cells[index].Add(new int2(l, i));
                        }
                    }
                }
            }
        }

        /// <summary>До какого расстояния от линии проверка по клетке точна, u.</summary>
        public float Margin { get; }

        /// <summary>
        /// Отрезки рядом с точкой (ближе Margin). false — точка вне сетки: тогда клетка ничего не знает,
        /// проверять надо по всей линии.
        /// </summary>
        public bool TryGetNear(float2 point, out List<int2> segments)
        {
            int2 cell = (int2)math.floor((point - origin) / cellSize);
            if (cell.x < 0 || cell.y < 0 || cell.x >= columns || cell.y >= rows)
            {
                segments = null;
                return false;
            }

            segments = cells[cell.y * columns + cell.x];
            return true;
        }
    }
}
