using System;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Config
{
    /// <summary>Все параметры баланса, которые получает симуляция. Разделы добавляются по этапам.</summary>
    [Serializable]
    public sealed class SimulationSettings
    {
        public SquadSettings squads = new SquadSettings();
        public UnitTypeSettings grenadiers = new UnitTypeSettings();
        public UnitTypeSettings fusiliers = new UnitTypeSettings();
        public UnitTypeSettings cavalry = new UnitTypeSettings();
        public UnitTypeSettings recruits = new UnitTypeSettings();
        public UnitTypeSettings artillery = new UnitTypeSettings();
        public GunSettings guns = new GunSettings();
        public CombatSettings combat = new CombatSettings();
        public MoraleSettings morale = new MoraleSettings();
        public MeleeSettings melee = new MeleeSettings();
        public AiSettings ai = new AiSettings();
        public EconomySettings economy = new EconomySettings();
        public BuildingSettings buildings = new BuildingSettings();
        public VictorySettings victory = new VictorySettings();
        public TerrainSettings terrain = new TerrainSettings();
        public WeatherSettings weather = new WeatherSettings();
        public VisionSettings vision = new VisionSettings();

        public UnitTypeSettings Unit(UnitType type)
        {
            switch (type)
            {
                case UnitType.Fusiliers:
                    return fusiliers;
                case UnitType.Cavalry:
                    return cavalry;
                case UnitType.Recruits:
                    return recruits;
                case UnitType.Artillery:
                    return artillery;
                default:
                    return grenadiers;
            }
        }
    }
}
