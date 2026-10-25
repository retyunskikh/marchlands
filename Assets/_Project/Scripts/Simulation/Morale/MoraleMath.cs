using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Morale
{
    /// <summary>Формула потери морали от потерь (раздел 3.3 docs/design.md).</summary>
    public static class MoraleMath
    {
        /// <summary>Верх шкалы морали; шкала 0–100 задана дизайном, а не балансом.</summary>
        public const float MaxMorale = 100f;

        /// <summary>
        /// Находится ли <paramref name="attackerPosition"/> во фланге или тылу цели — вне сектора
        /// ±frontalArcHalfAngle от её фронта (угол меряется из центра цели).
        /// </summary>
        public static bool IsFlank(Squad target, float2 attackerPosition, MoraleSettings settings)
        {
            float2 toAttacker = attackerPosition - target.Position;
            if (math.lengthsq(toAttacker) <= 0f)
            {
                return false;
            }

            return math.abs(Angles.Delta(target.Facing, Angles.Of(toAttacker))) > settings.frontalArcHalfAngle;
        }

        /// <summary>lossFraction × 100 × moraleShockFactor × (в упор) × (фланг/тыл).</summary>
        public static float Loss(int casualties, int aliveBefore, bool pointBlank, bool flank, MoraleSettings settings)
        {
            if (casualties <= 0 || aliveBefore <= 0)
            {
                return 0f;
            }

            float loss = (float)casualties / aliveBefore * MaxMorale * settings.moraleShockFactor;
            if (pointBlank)
            {
                loss *= settings.pointBlankMoraleMultiplier;
            }

            if (flank)
            {
                loss *= settings.flankMoraleMultiplier;
            }

            return loss;
        }

        internal static void ApplyLoss(Squad squad, int casualties, int aliveBefore, bool pointBlank, bool flank, MoraleSettings settings)
        {
            if (casualties <= 0)
            {
                return;
            }

            squad.TimeSinceLoss = 0f;
            squad.Morale = math.max(0f, squad.Morale - Loss(casualties, aliveBefore, pointBlank, flank, settings));
        }
    }
}
