using Marchlands.Simulation.Core;

namespace Marchlands.Simulation.Economy
{
    /// <summary>Казна стороны.</summary>
    public sealed class PlayerState
    {
        internal PlayerState(Side side, int gold)
        {
            Side = side;
            Gold = gold;
        }

        public Side Side { get; }

        public int Gold { get; internal set; }
    }
}
