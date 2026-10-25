using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Commands
{
    /// <summary>Построить из людей, ждущих у казарм, новый отряд выбранного рода войск; он встаёт у казармы.</summary>
    public sealed class FormSquadCommand : ICommand
    {
        public FormSquadCommand(Side side, UnitType type)
        {
            Side = side;
            Type = type;
        }

        public Side Side { get; }

        public UnitType Type { get; }

        public void Execute(Match match)
        {
            if (!BuildRules.CanFormSquad(match, Side, Type))
            {
                return;
            }

            match.GetPlayer(Side).Reserve -= match.Settings.Unit(Type).squadSize;
            BuildingSystem.SpawnSquad(match, BuildRules.GetFormingBarracks(match, Side), Type);
        }
    }
}
