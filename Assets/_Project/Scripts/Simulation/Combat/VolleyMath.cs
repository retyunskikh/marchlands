using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Combat
{
    /// <summary>Формула урона залпа (раздел 3.2 docs/design.md).</summary>
    public static class VolleyMath
    {
        /// <summary>Вероятность попадания одного стрелка на дистанции <paramref name="distance"/>; за maxRange — 0.</summary>
        public static float HitChance(float distance, CombatSettings settings)
        {
            if (distance <= settings.pointBlankRange)
            {
                return settings.hitPointBlank;
            }

            if (distance <= settings.effectiveRange)
            {
                float t = (distance - settings.pointBlankRange) / (settings.effectiveRange - settings.pointBlankRange);
                return math.lerp(settings.hitPointBlank, settings.hitEffective, t);
            }

            if (distance <= settings.maxRange)
            {
                float t = (distance - settings.effectiveRange) / (settings.maxRange - settings.effectiveRange);
                return math.lerp(settings.hitEffective, settings.hitMax, t);
            }

            return 0f;
        }

        /// <summary>
        /// Итоговая вероятность попадания с множителями первого залпа, колонны-цели и укрытия
        /// (<paramref name="coverMultiplier"/> — например, лес вокруг цели).
        /// </summary>
        public static float HitProbability(float distance, bool isFirstVolley, Formation targetFormation, CombatSettings settings, float coverMultiplier = 1f)
        {
            float p = HitChance(distance, settings) * coverMultiplier;
            if (isFirstVolley)
            {
                p *= settings.firstVolleyMultiplier;
            }

            if (targetFormation == Formation.Column)
            {
                p *= settings.columnTargetMultiplier;
            }

            return math.saturate(p);
        }

        /// <summary>Сколько солдат стреляет: в линии — все, в колонне — только первая шеренга.</summary>
        public static int Shooters(Formation formation, int alive, SquadSettings settings)
        {
            return formation == Formation.Line ? alive : math.min(alive, settings.columnFrontWidth);
        }

        /// <summary>Binomial(shooters, p) через <see cref="GameRandom"/>, но не больше живых в цели.</summary>
        public static int RollCasualties(GameRandom random, int shooters, float probability, int targetAlive)
        {
            int hits = 0;
            for (int i = 0; i < shooters; i++)
            {
                if (random.NextFloat() < probability)
                {
                    hits++;
                }
            }

            return math.min(hits, targetAlive);
        }
    }
}
