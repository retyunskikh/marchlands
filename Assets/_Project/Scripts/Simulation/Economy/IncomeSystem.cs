using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Unity.Mathematics;

namespace Marchlands.Simulation.Economy
{
    /// <summary>Доход: каждые incomeInterval секунд — villageIncome за каждую деревню стороны и hqIncome за штаб.</summary>
    internal static class IncomeSystem
    {
        public static void Update(Match match)
        {
            EconomySettings economy = match.Settings.economy;
            int intervalTicks = math.max(1, (int)math.round(economy.incomeInterval * SimulationTime.TicksPerSecond));

            // Первое начисление — в конце первого полного интервала.
            if ((match.CurrentTick + 1) % intervalTicks != 0)
            {
                return;
            }

            foreach (PlayerState player in match.Players)
            {
                int headquarters = match.GetHeadquarters(player.Side) != null ? 1 : 0;
                player.Gold += economy.villageIncome * match.CountVillages(player.Side) + economy.hqIncome * headquarters;
            }
        }
    }
}
