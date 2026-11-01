using Marchlands.Simulation.Core;

namespace Marchlands.View
{
    /// <summary>
    /// Какая нация у каждой стороны боя: от неё мундиры, портреты и названия. В обычном бою Франция — французы,
    /// Австрия — австрийцы; в сетевом каждый игрок выбирает нацию сам, и у обеих сторон она может совпасть
    /// (тогда цвет второй стороны — запасной, см. SquadViewStyle.GetSideColor).
    /// </summary>
    public static class Armies
    {
        private static readonly Nation[] nations = { Nation.France, Nation.Austria };

        public static Nation NationOf(Side side)
        {
            return nations[(int)side];
        }

        /// <summary>У обеих сторон одна нация.</summary>
        public static bool SameNation => nations[0] == nations[1];

        public static void Configure(Nation france, Nation austria)
        {
            nations[(int)Side.France] = france;
            nations[(int)Side.Austria] = austria;
        }

        /// <summary>Как в обычном бою: Франция — французы, Австрия — австрийцы.</summary>
        public static void Reset()
        {
            Configure(Nation.France, Nation.Austria);
        }
    }
}
