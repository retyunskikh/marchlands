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

        /// <summary>Почему залп невозможен; <see cref="VolleyBlock.None"/> — возможен.</summary>
        public static VolleyBlock GetBlock(Match match, Squad squad)
        {
            if (!CanStartAiming(match, squad))
            {
                return VolleyBlock.NotReady;
            }

            float range = UnitStats.MaxRange(squad, match.Settings);
            if (Targeting.FindTarget(match, squad, range) != null)
            {
                return VolleyBlock.None;
            }

            // Цели нет, но враг в секторе и дальности есть — значит, он в чужой свалке рукопашной.
            foreach (Squad other in match.Squads)
            {
                if (other.Side != squad.Side && other.Alive > 0 && other.IsInMelee
                    && FireGeometry.IsInSector(squad, other.Position, match.Settings)
                    && FireGeometry.Distance(squad, other, match.Settings.squads) <= range)
                {
                    return VolleyBlock.AllyInMelee;
                }
            }

            return VolleyBlock.NoTarget;
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
