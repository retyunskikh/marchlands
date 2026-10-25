using System;

namespace Marchlands.Simulation.Config
{
    /// <summary>Все параметры баланса, которые получает симуляция. Разделы добавляются по этапам.</summary>
    [Serializable]
    public sealed class SimulationSettings
    {
        public SquadSettings squads = new SquadSettings();
        public CombatSettings combat = new CombatSettings();
        public MoraleSettings morale = new MoraleSettings();
        public MeleeSettings melee = new MeleeSettings();
        public AiSettings ai = new AiSettings();
        public EconomySettings economy = new EconomySettings();
        public BuildingSettings buildings = new BuildingSettings();
        public VictorySettings victory = new VictorySettings();
    }
}
