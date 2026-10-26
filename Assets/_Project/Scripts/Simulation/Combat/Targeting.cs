using Marchlands.Simulation.Core;
using Marchlands.Simulation.Melee;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Combat
{
    /// <summary>Выбор цели: ближайший вражеский отряд в секторе огня и в пределах дальности.</summary>
    public static class Targeting
    {
        /// <summary>Ближайшая цель не дальше <paramref name="range"/>; при равенстве — с меньшим Id. null — целей нет.</summary>
        public static Squad FindTarget(Match match, Squad shooter, float range)
        {
            Squad best = null;
            float bestDistance = float.MaxValue;

            foreach (Squad candidate in match.Squads)
            {
                if (!IsValidTarget(match, shooter, candidate, range, out float distance))
                {
                    continue;
                }

                if (distance < bestDistance || (distance == bestDistance && candidate.Id < best.Id))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best;
        }

        public static bool IsValidTarget(Match match, Squad shooter, Squad candidate, float range, out float distance)
        {
            distance = float.MaxValue;
            if (candidate == null || candidate.Side == shooter.Side || candidate.Alive <= 0)
            {
                return false;
            }

            // В чужую свалку рукопашной не стреляют: там и свои. Своего противника в рукопашной бьют в упор с любой стороны.
            bool ownOpponent = candidate.IsInMelee && shooter.IsInMelee && MeleeSystem.AreEngaged(match, shooter, candidate);
            if (candidate.IsInMelee && !ownOpponent)
            {
                return false;
            }

            if (!ownOpponent && !FireGeometry.IsInSector(shooter, candidate.Position, match.Settings))
            {
                return false;
            }

            distance = FireGeometry.Distance(shooter, candidate, match.Settings.squads);
            return distance <= range;
        }
    }
}
