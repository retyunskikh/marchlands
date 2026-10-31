using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Commands
{
    /// <summary>Зарядить пушку ядром или картечью. Начатая наводка сбивается; перезарядка — нет.</summary>
    public sealed class SetShotModeCommand : ICommand
    {
        public SetShotModeCommand(int squadId, ShotMode mode)
        {
            SquadId = squadId;
            Mode = mode;
        }

        public int SquadId { get; }

        public ShotMode Mode { get; }

        public void Execute(Match match)
        {
            Squad squad = match.FindSquad(SquadId);
            if (squad == null || !squad.IsArtillery || !squad.CanReceiveOrders || squad.ShotMode == Mode)
            {
                return;
            }

            squad.ShotMode = Mode;
            ArtillerySystem.CancelAiming(squad);
        }
    }
}
