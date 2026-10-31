using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Commands
{
    /// <summary>
    /// Указать пушке цель — вражеский отряд или здание (ядром): пока цель цела и её можно достать, пушка бьёт
    /// по ней, а не по ближайшему врагу. 0 — снова по ближайшему.
    /// </summary>
    public sealed class TargetCommand : ICommand
    {
        public TargetCommand(int squadId, int targetSquadId)
        {
            SquadId = squadId;
            TargetSquadId = targetSquadId;
        }

        public int SquadId { get; }

        public int TargetSquadId { get; }

        /// <summary>Вражеское здание целью (обстрел ядрами).</summary>
        public int TargetBuildingId { get; private set; }

        public static TargetCommand AtBuilding(int squadId, int buildingId)
        {
            return new TargetCommand(squadId, 0) { TargetBuildingId = buildingId };
        }

        public void Execute(Match match)
        {
            Squad squad = match.FindSquad(SquadId);
            if (squad == null || !squad.IsArtillery || !squad.CanReceiveOrders)
            {
                return;
            }

            if (TargetBuildingId != 0)
            {
                Buildings.Building building = match.FindBuilding(TargetBuildingId);
                squad.PreferredTargetId = building != null && building.Owner != squad.Side ? -building.Id : 0;
                ArtillerySystem.CancelAiming(squad);
                return;
            }

            Squad target = match.FindSquad(TargetSquadId);
            squad.PreferredTargetId = target != null && target.Side != squad.Side ? target.Id : 0;
            ArtillerySystem.CancelAiming(squad);
        }
    }
}
