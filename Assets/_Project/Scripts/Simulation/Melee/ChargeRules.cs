using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Melee
{
    /// <summary>Можно ли отряду атаковать в штыки. По этому правилу включается кнопка «В штыки».</summary>
    public static class ChargeRules
    {
        public static bool CanCharge(Squad squad)
        {
            // Расчёт пушки в атаку не ходит — только отбивается.
            return squad != null && squad.Alive > 0 && squad.CanReceiveOrders && !squad.IsChangingFormation && !squad.IsArtillery;
        }

        public static bool CanCharge(Squad squad, Squad target)
        {
            return CanCharge(squad) && IsValidTarget(squad, target);
        }

        /// <summary>Можно ли отряду пойти в штыки на здание: оно вражеское и ещё не разрушено. Камп рекрутов на штурм не ходит.</summary>
        public static bool CanAssault(Squad squad, Building building)
        {
            return CanCharge(squad) && !squad.IsCamp && building != null && building.Owner != squad.Side && !building.IsDestroyed;
        }

        internal static bool IsValidTarget(Squad squad, Squad target)
        {
            return target != null && target.Side != squad.Side && target.Alive > 0 && !target.IsRouting && !target.IsWithdrawn;
        }
    }
}
