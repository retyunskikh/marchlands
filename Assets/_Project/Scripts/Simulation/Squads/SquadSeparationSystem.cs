using System.Collections.Generic;
using Marchlands.Simulation.Config;
using Unity.Mathematics;

namespace Marchlands.Simulation.Squads
{
    /// <summary>
    /// Простое отталкивание: пересекающиеся отряды расталкиваются по оси наименьшего перекрытия.
    /// Бегущие проходят сквозь своих (через интервалы порядков), отряд в рукопашной не сдвигается с места.
    /// Пары обходятся в порядке списка, поэтому результат детерминирован.
    /// </summary>
    internal static class SquadSeparationSystem
    {
        public static void Resolve(IReadOnlyList<Squad> squads, SquadSettings settings)
        {
            for (int i = 0; i < squads.Count; i++)
            {
                for (int j = i + 1; j < squads.Count; j++)
                {
                    Squad a = squads[i];
                    Squad b = squads[j];
                    if (a.Side == b.Side && (a.IsRouting || b.IsRouting))
                    {
                        continue;
                    }

                    if (!SquadFootprint.TryGetPenetration(SquadFootprint.Of(a, settings), SquadFootprint.Of(b, settings), out float2 push))
                    {
                        continue;
                    }

                    float weightA = a.IsInMelee ? 0f : 1f;
                    float weightB = b.IsInMelee ? 0f : 1f;
                    if (weightA + weightB <= 0f)
                    {
                        weightA = 1f;
                        weightB = 1f;
                    }

                    float total = weightA + weightB;
                    a.Position -= push * (weightA / total);
                    b.Position += push * (weightB / total);
                }
            }
        }
    }
}
