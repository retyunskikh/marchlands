using System.Collections.Generic;
using Marchlands.Simulation.Config;
using Unity.Mathematics;

namespace Marchlands.Simulation.Squads
{
    /// <summary>
    /// Простое отталкивание: пересекающиеся отряды расталкиваются поровну по оси наименьшего перекрытия.
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
                    if (SquadFootprint.TryGetPenetration(SquadFootprint.Of(a, settings), SquadFootprint.Of(b, settings), out float2 push))
                    {
                        a.Position -= push * 0.5f;
                        b.Position += push * 0.5f;
                    }
                }
            }
        }
    }
}
