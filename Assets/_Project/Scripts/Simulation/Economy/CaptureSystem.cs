using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Economy
{
    /// <summary>
    /// Захват деревень (раздел 3.5 docs/design.md): прогресс растёт, пока в радиусе только одна сторона
    /// (не владелец); замирает, если там обе; убывает, если там никого нет или только владелец.
    /// Бегущие не захватывают и не мешают. Новый захватчик начинает с нуля.
    /// </summary>
    internal static class CaptureSystem
    {
        // Прогресс складывается из шагов 0.1 с во float; допуск не даёт лишнего тика из-за ошибки округления.
        private const float ProgressEpsilon = 1e-4f;

        public static void Update(Match match, float deltaTime)
        {
            EconomySettings economy = match.Settings.economy;

            foreach (Village village in match.Villages)
            {
                GetPresence(match, village, economy.captureRadius, out bool france, out bool austria);

                if (france && austria)
                {
                    continue;
                }

                Side? present = france ? Side.France : austria ? Side.Austria : (Side?)null;
                if (present == null || present == village.Owner)
                {
                    Decay(village, economy, deltaTime);
                    continue;
                }

                if (village.CapturingSide != present)
                {
                    village.CapturingSide = present;
                    village.CaptureProgress = 0f;
                }

                village.CaptureProgress += deltaTime;
                if (village.CaptureProgress >= economy.captureTime - ProgressEpsilon)
                {
                    village.Owner = present;
                    village.CapturingSide = null;
                    village.CaptureProgress = 0f;
                    match.RecordCapture(village);
                }
            }
        }

        /// <summary>Есть ли в радиусе деревни не бегущие отряды каждой стороны.</summary>
        public static void GetPresence(Match match, Village village, float radius, out bool france, out bool austria)
        {
            france = false;
            austria = false;
            foreach (Squad squad in match.Squads)
            {
                if (squad.IsRouting || squad.Alive <= 0 || math.distance(squad.Position, village.Position) > radius)
                {
                    continue;
                }

                if (squad.Side == Side.France)
                {
                    france = true;
                }
                else
                {
                    austria = true;
                }
            }
        }

        private static void Decay(Village village, EconomySettings economy, float deltaTime)
        {
            if (village.CapturingSide == null)
            {
                return;
            }

            village.CaptureProgress = math.max(0f, village.CaptureProgress - economy.captureDecayRate * deltaTime);
            if (village.CaptureProgress <= 0f)
            {
                village.CapturingSide = null;
            }
        }
    }
}
