using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Melee
{
    /// <summary>Рукопашная пары отрядов: кто атаковал, с какого тика и пришёлся ли удар во фланг или тыл.</summary>
    public sealed class MeleeEngagement
    {
        internal MeleeEngagement(Squad attacker, Squad defender, int startTick, bool isFlank)
        {
            Attacker = attacker;
            Defender = defender;
            StartTick = startTick;
            IsFlank = isFlank;
        }

        public Squad Attacker { get; }

        public Squad Defender { get; }

        public int StartTick { get; }

        public bool IsFlank { get; }

        public bool Involves(Squad squad)
        {
            return Attacker == squad || Defender == squad;
        }
    }
}
