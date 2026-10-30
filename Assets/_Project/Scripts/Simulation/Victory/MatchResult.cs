using Marchlands.Simulation.Core;

namespace Marchlands.Simulation.Victory
{
    public enum VictoryReason
    {
        HeadquartersDestroyed,
        Domination,
        Annihilation
    }

    /// <summary>Итог матча: кто победил, почему и на каком тике.</summary>
    public sealed class MatchResult
    {
        internal MatchResult(Side winner, VictoryReason reason, int tick)
        {
            Winner = winner;
            Reason = reason;
            Tick = tick;
        }

        public Side Winner { get; }

        public VictoryReason Reason { get; }

        public int Tick { get; }
    }
}
