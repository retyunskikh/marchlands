using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Economy
{
    /// <summary>
    /// Пополнение из деревень: не бегущий пехотный отряд в зоне своей деревни раз в reinforceInterval получает
    /// одного человека, пока не наберёт полную численность своего рода войск. Даже в бою; конница так не пополняется.
    /// </summary>
    internal static class ReinforcementSystem
    {
        // Таймеры складываются из шагов 0.1 с во float; допуск не даёт лишнего тика из-за ошибки округления.
        private const float TimerEpsilon = 1e-4f;

        public static void Update(Match match, float deltaTime)
        {
            float interval = match.Settings.economy.reinforceInterval;
            foreach (Squad squad in match.Squads)
            {
                Village village = interval > 0f && CanReinforce(match, squad) ? FindOwnVillage(match, squad) : null;
                squad.IsReinforcing = village != null;
                if (village == null)
                {
                    squad.ReinforceTimer = 0f;
                    squad.ReinforceProgress = 0f;
                    continue;
                }

                squad.ReinforceTimer += deltaTime;
                if (squad.ReinforceTimer + TimerEpsilon < interval)
                {
                    squad.ReinforceProgress = squad.ReinforceTimer / interval;
                    continue;
                }

                squad.ReinforceTimer = 0f;
                squad.ReinforceProgress = 0f;
                squad.Alive++;
                squad.IsReinforcing = squad.Alive < UnitStats.Of(squad, match.Settings).squadSize;
                match.ReportReinforcement(squad, village);
            }
        }

        private static bool CanReinforce(Match match, Squad squad)
        {
            return squad.Type != UnitType.Cavalry
                && !squad.IsRouting
                && !squad.IsWithdrawn
                && squad.Alive > 0
                && squad.Alive < UnitStats.Of(squad, match.Settings).squadSize;
        }

        private static Village FindOwnVillage(Match match, Squad squad)
        {
            float radius = match.Settings.economy.captureRadius;
            foreach (Village village in match.Villages)
            {
                if (village.Owner == squad.Side && math.distance(village.Position, squad.Position) <= radius)
                {
                    return village;
                }
            }

            return null;
        }
    }
}
