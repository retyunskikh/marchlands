using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Combat
{
    /// <summary>Можно ли сейчас скомандовать залп. По этому правилу включается кнопка «Залп!».</summary>
    public static class VolleyRules
    {
        public static bool CanVolley(Match match, Squad squad)
        {
            return CanStartAiming(squad)
                && Targeting.FindTarget(match, squad, match.Settings.combat.maxRange) != null;
        }

        internal static bool CanStartAiming(Squad squad)
        {
            return squad != null
                && squad.Alive > 0
                && squad.FireState == FireState.Loaded
                && !squad.IsChangingFormation;
        }
    }
}
