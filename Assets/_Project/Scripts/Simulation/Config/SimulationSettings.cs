using System;

namespace Marchlands.Simulation.Config
{
    /// <summary>Все параметры баланса, которые получает симуляция. Разделы добавляются по этапам.</summary>
    [Serializable]
    public sealed class SimulationSettings
    {
        public SquadSettings squads = new SquadSettings();
        public CombatSettings combat = new CombatSettings();
    }
}
