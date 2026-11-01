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

        /// <summary>Прибавляет мораль (не выше максимума); бегущим и толпе рекрутов — нет.</summary>
        internal static void ApplyGain(Squad squad, float gain)
        {
            if (gain <= 0f || squad == null || squad.IsRouting || squad.IsCamp || squad.Alive <= 0)
            {
                return;
            }

            squad.Morale = math.min(squad.MaxMorale, squad.Morale + gain);
        }

        /// <summary>Убил врагов — дух крепнет (killMoraleGain за каждого), отряд набирается опыта.</summary>
        internal static void ApplyKills(Squad squad, float kills, SimulationSettings settings)
        {
            if (squad == null || kills <= 0f || squad.IsCamp)
            {
                return;
            }

            squad.Kills += kills;
            squad.MaxMorale = GetMaxMorale(squad.Kills, settings.morale);
            squad.VeteranRank = math.max(squad.VeteranRank, GetVeteranRank(squad.Kills, settings.morale));
            ApplyGain(squad, kills * settings.morale.killMoraleGain);
        }

        /// <summary>Предел морали при таком опыте: 100 + veteranMaxMoraleBonus × (1 − e^(−убито / veteranKillScale)).</summary>
        public static float GetMaxMorale(float kills, MoraleSettings settings)
        {
            if (kills <= 0f || settings.veteranKillScale <= 0f || settings.veteranMaxMoraleBonus <= 0f)
            {
                return MaxMorale;
            }

            return MaxMorale + settings.veteranMaxMoraleBonus * (1f - math.exp(-kills / settings.veteranKillScale));
        }

        /// <summary>Степень ветерана по числу убитых: 0–3.</summary>
        public static int GetVeteranRank(float kills, MoraleSettings settings)
        {
            if (settings.veteranRank3Kills > 0f && kills >= settings.veteranRank3Kills)
            {
                return 3;
            }

            if (settings.veteranRank2Kills > 0f && kills >= settings.veteranRank2Kills)
            {
                return 2;
            }

            return settings.veteranRank1Kills > 0f && kills >= settings.veteranRank1Kills ? 1 : 0;
        }

        /// <summary>
        /// Потеря морали в секунду от давления: людей у не отступающих врагов в pressureRadius больше, чем у нас
        /// с союзниками, в pressureRatio раз и более — pressureMoraleLoss × (перевес / порог − 1), не больше pressureMaxLoss.
        /// </summary>
        public static float PressureLoss(float enemyMen, float friendlyMen, MoraleSettings settings)
        {
            if (settings.pressureMoraleLoss <= 0f || settings.pressureRatio <= 0f || enemyMen <= 0f)
            {
                return 0f;
            }

            float ratio = enemyMen / math.max(1f, friendlyMen);
            float excess = ratio / settings.pressureRatio - 1f;
            if (excess <= 0f)
            {
                return 0f;
            }

            float loss = settings.pressureMoraleLoss * excess;
            return settings.pressureMaxLoss > 0f ? math.min(loss, settings.pressureMaxLoss) : loss;
        }

        /// <summary>Снимает мораль за потери с учётом рода войск (гренадёры крепче).</summary>
        internal static void ApplyLoss(Squad squad, int casualties, int aliveBefore, bool pointBlank, bool flank, SimulationSettings settings)
        {
            if (casualties <= 0)
            {
                return;
            }

            float loss = Loss(casualties, aliveBefore, pointBlank, flank, settings.morale) * UnitStats.Of(squad, settings).moraleLossMultiplier;
            squad.TimeSinceLoss = 0f;
            squad.Morale = math.max(0f, squad.Morale - loss);
        }
    }
}
