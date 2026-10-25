using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Commands
{
    /// <summary>«Залп!»: начать прицеливание по ближайшей цели. Игнорируется, если залп сейчас невозможен.</summary>
    public sealed class VolleyCommand : ICommand
    {
        public VolleyCommand(int squadId)
            : this(squadId, float.MaxValue)
        {
        }

        /// <param name="maxDistance">
        /// Не стрелять, если ближайшая цель дальше: так ИИ держит огонь до эффективной дальности,
        /// даже если цель сместилась, пока приказ ждал исполнения.
        /// </param>
        public VolleyCommand(int squadId, float maxDistance)
        {
            SquadId = squadId;
            MaxDistance = maxDistance;
        }

        public int SquadId { get; }

        public float MaxDistance { get; }

        public void Execute(Match match)
        {
            Squad squad = match.FindSquad(SquadId);
            if (!VolleyRules.CanVolley(match, squad))
            {
                return;
            }

            float range = System.Math.Min(MaxDistance, match.Settings.combat.maxRange);
            Squad target = Targeting.FindTarget(match, squad, range);
            if (target != null)
            {
                FireSystem.StartAiming(squad, target, match.Settings.combat, range);
            }
        }
    }
}
