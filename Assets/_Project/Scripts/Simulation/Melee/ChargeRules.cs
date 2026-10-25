using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Melee
{
    /// <summary>Можно ли отряду атаковать в штыки. По этому правилу включается кнопка «В штыки».</summary>
    public static class ChargeRules
    {
        public static bool CanCharge(Squad squad)
        {
            return squad != null && squad.Alive > 0 && squad.CanReceiveOrders && !squad.IsChangingFormation;
        }

        public static bool CanCharge(Squad squad, Squad target)
        {
            return CanCharge(squad) && IsValidTarget(squad, target);
        }

        internal static bool IsValidTarget(Squad squad, Squad target)
        {
            return target != null && target.Side != squad.Side && target.Alive > 0 && !target.IsRouting && !target.IsDisbanded;
        }
    }
}
