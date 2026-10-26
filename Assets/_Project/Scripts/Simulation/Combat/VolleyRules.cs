using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Combat
{
    /// <summary>Можно ли сейчас скомандовать залп. По этому правилу включается кнопка «Залп!».</summary>
    public static class VolleyRules
    {
        public static bool CanVolley(Match match, Squad squad)
        {
            return CanStartAiming(match, squad)
                && Targeting.FindTarget(match, squad, UnitStats.MaxRange(squad, match.Settings)) != null;
        }

        internal static bool CanStartAiming(Match match, Squad squad)
        {
            // Конница и бегущие не стреляют; бегущие в штыки дают залп на подходе, дерущиеся — в упор по своему противнику.
            return squad != null
                && UnitStats.CanFire(squad, match.Settings)
                && squad.Alive > 0
                && !squad.IsRouting
                && squad.FireState == FireState.Loaded
                && !squad.IsChangingFormation;
        }
    }
}
