namespace Marchlands.Simulation.Economy
{
    /// <summary>В отряд пришёл человек из деревни.</summary>
    public readonly struct Reinforcement
    {
        public Reinforcement(int squadId, int villageId)
        {
            SquadId = squadId;
            VillageId = villageId;
        }

        public int SquadId { get; }

        public int VillageId { get; }
    }
}
