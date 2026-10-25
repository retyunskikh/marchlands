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

        /// <summary>Люди, ждущие у казарм: рекруты и солдаты разбитых отрядов, добежавшие до своих. Из них формируются отряды.</summary>
        public int Reserve { get; internal set; }

        /// <summary>Всадники, ждущие у конюшен: нанятые и добежавшие из разбитых отрядов конницы.</summary>
        public int CavalryReserve { get; internal set; }

        /// <summary>Резерв, из которого формируется отряд этого рода войск.</summary>
        public int GetReserve(Squads.UnitType type)
        {
            return type == Squads.UnitType.Cavalry ? CavalryReserve : Reserve;
        }

        internal void AddReserve(Squads.UnitType type, int count)
        {
            if (type == Squads.UnitType.Cavalry)
            {
                CavalryReserve += count;
            }
            else
            {
                Reserve += count;
            }
        }
    }
}
