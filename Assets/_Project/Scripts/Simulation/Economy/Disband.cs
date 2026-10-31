using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Economy
{
    /// <summary>Сторона распустила ждущих у казармы или конюшни людей одного рода войск.</summary>
    public readonly struct Disband
    {
        public Disband(Side side, UnitType type, int count)
        {
            Side = side;
            Type = type;
            Count = count;
        }

        public Side Side { get; }

        public UnitType Type { get; }

        public int Count { get; }
    }
}
