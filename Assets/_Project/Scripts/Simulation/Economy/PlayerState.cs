using Marchlands.Simulation.Core;

namespace Marchlands.Simulation.Economy
{
    /// <summary>Казна и резерв стороны.</summary>
    public sealed class PlayerState
    {
        internal PlayerState(Side side, int gold)
        {
            Side = side;
            Gold = gold;
        }

        public Side Side { get; }

        public int Gold { get; internal set; }

        /// <summary>Солдаты разбитых отрядов, добежавшие до своих; из них бесплатно собирается новый отряд.</summary>
        public int Reserve { get; internal set; }
    }
}
