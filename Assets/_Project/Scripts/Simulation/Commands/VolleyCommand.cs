using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Commands
{
    /// <summary>«Залп!»: начать прицеливание по ближайшей цели. Игнорируется, если залп сейчас невозможен.</summary>
    public sealed class VolleyCommand : ICommand
    {
        public VolleyCommand(int squadId)
        {
            SquadId = squadId;
        }

        public int SquadId { get; }

        public void Execute(Match match)
        {
            Squad squad = match.FindSquad(SquadId);
            if (!VolleyRules.CanVolley(match, squad))
            {
                return;
            }

            Squad target = Targeting.FindTarget(match, squad, match.Settings.combat.maxRange);
            FireSystem.StartAiming(squad, target, match.Settings.combat);
        }
    }
}
