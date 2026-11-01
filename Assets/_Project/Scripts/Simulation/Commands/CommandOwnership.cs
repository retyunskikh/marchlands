using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Commands
{
    /// <summary>
    /// Чья команда: в сетевой игре сторона может приказывать только своим отрядам и зданиям и только от своего
    /// имени. Чужие команды отбрасываются одинаково на обеих машинах — симуляция не расходится.
    /// </summary>
    public static class CommandOwnership
    {
        public static bool IsAllowed(Match match, ICommand command, Side side)
        {
            switch (command)
            {
                case MoveCommand move:
                    return OwnsSquad(match, move.SquadId, side);
                case FaceCommand face:
                    return OwnsSquad(match, face.SquadId, side);
                case StopCommand stop:
                    return OwnsSquad(match, stop.SquadId, side);
                case VolleyCommand volley:
                    return OwnsSquad(match, volley.SquadId, side);
                case ChargeCommand charge:
                    return OwnsSquad(match, charge.SquadId, side);
                case TargetCommand target:
                    return OwnsSquad(match, target.SquadId, side);
                case AssaultCommand assault:
                    return OwnsSquad(match, assault.SquadId, side);
                case ChangeFormationCommand formation:
                    return OwnsSquad(match, formation.SquadId, side);
                case SetShotModeCommand shotMode:
                    return OwnsSquad(match, shotMode.SquadId, side);
                case BuildBarracksCommand barracks:
                    return barracks.Side == side;
                case BuildStableCommand stable:
                    return stable.Side == side;
                case BuildDepotCommand depot:
                    return depot.Side == side;
                case BuildHouseCommand house:
                    return house.Side == side;
                case FormSquadCommand form:
                    return form.Side == side;
                case DisbandReserveCommand disband:
                    return disband.Side == side;
                case ResearchBalloonCommand balloon:
                    return balloon.Side == side;
                case SetHiringCommand hiring:
                    return OwnsBuilding(match, hiring.BuildingId, side);
                case OrderGunCommand gun:
                    return OwnsBuilding(match, gun.BuildingId, side);
                case UpgradeBuildingCommand upgrade:
                    return OwnsBuilding(match, upgrade.BuildingId, side);
                default:
                    return false;
            }
        }

        private static bool OwnsSquad(Match match, int squadId, Side side)
        {
            Squad squad = match.FindSquad(squadId);
            return squad != null && squad.Side == side;
        }

        private static bool OwnsBuilding(Match match, int buildingId, Side side)
        {
            Building building = match.FindBuilding(buildingId);
            return building != null && building.Owner == side;
        }
    }
}
