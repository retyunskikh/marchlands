using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Commands
{
    /// <summary>
    /// Распустить людей резерва одного рода войск, ждущих у казармы или конюшни: резерв обнуляется, места населения
    /// освобождаются сразу. Найм этого рода войск выключается, иначе резерв тут же начал бы набираться снова.
    /// Золото не возвращается.
    /// </summary>
    public sealed class DisbandReserveCommand : ICommand
    {
        public DisbandReserveCommand(Side side, UnitType type)
        {
            Side = side;
            Type = type;
        }

        public Side Side { get; }

        public UnitType Type { get; }

        public void Execute(Match match)
        {
            PlayerState player = match.GetPlayer(Side);
            int count = player.GetReserve(Type);
            if (Type == UnitType.Recruits || count <= 0)
            {
                return;
            }

            player.AddReserve(Type, -count);
            foreach (Building building in match.Buildings)
            {
                if (building.Owner == Side && BuildRules.CanHireType(building, Type))
                {
                    building.SetHiring(Type, false);
                }
            }

            match.ReportDisband(new Disband(Side, Type, count));
        }
    }
}
