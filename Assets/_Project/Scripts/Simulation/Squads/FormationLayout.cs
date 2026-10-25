using System.Collections.Generic;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Unity.Mathematics;

namespace Marchlands.Simulation.Squads
{
    /// <summary>
    /// Геометрия строя: сетка шеренг, слоты солдат и габариты. Локальные координаты отряда:
    /// x — вправо по фронту, y — вперёд; центр габаритов в начале координат.
    /// </summary>
    public static class FormationLayout
    {
        public static void GetGrid(Formation formation, int alive, SquadSettings settings, out int files, out int ranks)
        {
            if (alive <= 0)
            {
                files = 0;
                ranks = 0;
                return;
            }

            files = formation == Formation.Line
                ? CeilDiv(alive, settings.lineRanks)
                : math.min(alive, settings.columnFrontWidth);
            ranks = CeilDiv(alive, files);
        }

        /// <summary>Половины ширины (x) и глубины (y) прямоугольника, который занимает отряд.</summary>
        public static float2 GetHalfExtents(Formation formation, int alive, SquadSettings settings)
        {
            GetGrid(formation, alive, settings, out int files, out int ranks);
            return new float2(files * settings.fileSpacing, ranks * settings.rankSpacing) * 0.5f;
        }

        /// <summary>
        /// Заполняет <paramref name="result"/> локальными позициями солдат: первой идёт передняя шеренга,
        /// неполная последняя шеренга выравнивается по центру. Число слотов равно <paramref name="alive"/>.
        /// </summary>
        public static void GetLocalSlots(Formation formation, int alive, SquadSettings settings, List<float2> result)
        {
            result.Clear();
            GetGrid(formation, alive, settings, out int files, out int ranks);

            for (int i = 0; i < alive; i++)
            {
                int rank = i / files;
                int file = i % files;
                int soldiersInRank = math.min(files, alive - rank * files);

                float x = (file - (soldiersInRank - 1) * 0.5f) * settings.fileSpacing;
                float y = ((ranks - 1) * 0.5f - rank) * settings.rankSpacing;
                result.Add(new float2(x, y));
            }
        }

        public static float2 LocalToWorld(float2 local, float2 position, float facing)
        {
            return position + Angles.Right(facing) * local.x + Angles.Direction(facing) * local.y;
        }

        private static int CeilDiv(int value, int divisor)
        {
            return (value + divisor - 1) / divisor;
        }
    }
}
