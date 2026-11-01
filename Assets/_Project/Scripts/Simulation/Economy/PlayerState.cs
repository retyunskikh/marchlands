using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Economy
{
    /// <summary>Казна и резервы стороны: у каждого рода войск — свой запас людей, ждущих у казарм и конюшен.</summary>
    public sealed class PlayerState
    {
        private readonly int[] reserves = new int[5];

        internal PlayerState(Side side, int gold)
        {
            Side = side;
            Gold = gold;
        }

        public Side Side { get; }

        public int Gold { get; internal set; }

        /// <summary>Воздушный шар изучен: туман войны для стороны снят.</summary>
        public bool HasBalloon { get; internal set; }

        /// <summary>Сколько ещё изучать шар, s; 0 — не изучают.</summary>
        public float BalloonResearchRemaining { get; internal set; }

        public bool IsResearchingBalloon => BalloonResearchRemaining > 0f;

        /// <summary>Рекруты и вернувшиеся из разбитых отрядов гренадёры.</summary>
        public int GrenadierReserve
        {
            get => reserves[(int)UnitType.Grenadiers];
            internal set => reserves[(int)UnitType.Grenadiers] = value;
        }

        /// <summary>Рекруты и вернувшиеся из разбитых отрядов фузилёры.</summary>
        public int FusilierReserve
        {
            get => reserves[(int)UnitType.Fusiliers];
            internal set => reserves[(int)UnitType.Fusiliers] = value;
        }

        /// <summary>Всадники, ждущие у конюшен: нанятые и добежавшие из разбитых отрядов конницы.</summary>
        public int CavalryReserve
        {
            get => reserves[(int)UnitType.Cavalry];
            internal set => reserves[(int)UnitType.Cavalry] = value;
        }

        /// <summary>Пушкари, ждущие у артдепо: из них составляются пушки.</summary>
        public int ArtilleryReserve
        {
            get => reserves[(int)UnitType.Artillery];
            internal set => reserves[(int)UnitType.Artillery] = value;
        }

        /// <summary>Резерв, из которого формируется отряд этого рода войск.</summary>
        public int GetReserve(UnitType type)
        {
            return reserves[(int)type];
        }

        internal void AddReserve(UnitType type, int count)
        {
            reserves[(int)type] += count;
        }
    }
}
