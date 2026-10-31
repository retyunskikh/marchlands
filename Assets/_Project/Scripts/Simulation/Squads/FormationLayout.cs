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
        /// <param name="maxFiles">Не шире стольких рядов (узкое место, например мост): строй становится глубже; 0 — без ограничения.</param>
        public static void GetGrid(Formation formation, int alive, SquadSettings settings, out int files, out int ranks, int maxFiles = 0)
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
            if (maxFiles > 0)
            {
                files = math.min(files, maxFiles);
            }

            ranks = CeilDiv(alive, files);
        }

        /// <summary>Сетка строя отряда с учётом сжатия (<see cref="Squad.FileLimit"/>).</summary>
        public static void GetGrid(Squad squad, SquadSettings settings, out int files, out int ranks)
        {
            GetGrid(squad.Formation, squad.Alive, settings, out files, out ranks, squad.FileLimit);
        }

        /// <summary>Интервал между солдатами в шеренге строя: колонна идёт плотнее, плечом к плечу.</summary>
        public static float FileSpacing(Formation formation, SquadSettings settings)
        {
            return formation == Formation.Column && settings.columnFileSpacing > 0f ? settings.columnFileSpacing : settings.fileSpacing;
        }

        /// <summary>Половины ширины (x) и глубины (y) прямоугольника, который занимает отряд.</summary>
        public static float2 GetHalfExtents(Formation formation, int alive, SquadSettings settings, int maxFiles = 0)
        {
            GetGrid(formation, alive, settings, out int files, out int ranks, maxFiles);
            return new float2(files * FileSpacing(formation, settings), ranks * settings.rankSpacing) * 0.5f;
        }

        /// <summary>Габариты отряда в его нынешнем строю с учётом сжатия.</summary>
        public static float2 GetHalfExtents(Squad squad, SquadSettings settings)
        {
            return GetHalfExtents(squad.Formation, squad.Alive, settings, squad.FileLimit);
        }

        /// <summary>
        /// Заполняет <paramref name="result"/> локальными позициями солдат: первой идёт передняя шеренга,
        /// неполная последняя шеренга выравнивается по центру. Число слотов равно <paramref name="alive"/>.
        /// </summary>
        public static void GetLocalSlots(Formation formation, int alive, SquadSettings settings, List<float2> result, int maxFiles = 0)
        {
            result.Clear();
            GetGrid(formation, alive, settings, out int files, out int ranks, maxFiles);
            float fileSpacing = FileSpacing(formation, settings);

            for (int i = 0; i < alive; i++)
            {
                int rank = i / files;
                int file = i % files;
                int soldiersInRank = math.min(files, alive - rank * files);

                float x = (file - (soldiersInRank - 1) * 0.5f) * fileSpacing;
                float y = ((ranks - 1) * 0.5f - rank) * settings.rankSpacing;
                result.Add(new float2(x, y));
            }
        }

        /// <summary>
        /// Сопоставляет слоты двух строёв для перестроения: <paramref name="targetForSource"/>[i] — слот
        /// в <paramref name="toSlots"/>, куда идёт солдат из <paramref name="fromSlots"/>[i]. Оба строя
        /// упорядочиваются слева направо (а внутри ряда — от фронта к тылу) и сопоставляются по порядку,
        /// поэтому строй сжимается или раздаётся вбок без пересечения путей.
        /// </summary>
        public static void MatchTransitionSlots(List<float2> fromSlots, List<float2> toSlots, List<int> targetForSource)
        {
            if (fromSlots.Count != toSlots.Count)
            {
                throw new System.ArgumentException("Число слотов в обоих строях должно совпадать.");
            }

            List<int> fromOrder = SortLeftToRight(fromSlots);
            List<int> toOrder = SortLeftToRight(toSlots);

            targetForSource.Clear();
            for (int i = 0; i < fromSlots.Count; i++)
            {
                targetForSource.Add(0);
            }

            for (int k = 0; k < fromOrder.Count; k++)
            {
                targetForSource[fromOrder[k]] = toOrder[k];
            }
        }

        public static float2 LocalToWorld(float2 local, float2 position, float facing)
        {
            return position + Angles.Right(facing) * local.x + Angles.Direction(facing) * local.y;
        }

        private static List<int> SortLeftToRight(List<float2> slots)
        {
            var order = new List<int>(slots.Count);
            for (int i = 0; i < slots.Count; i++)
            {
                order.Add(i);
            }

            order.Sort((a, b) =>
            {
                int byX = slots[a].x.CompareTo(slots[b].x);
                if (byX != 0)
                {
                    return byX;
                }

                int byFront = slots[b].y.CompareTo(slots[a].y);
                return byFront != 0 ? byFront : a.CompareTo(b);
            });
            return order;
        }

        private static int CeilDiv(int value, int divisor)
        {
            return (value + divisor - 1) / divisor;
        }
    }
}
