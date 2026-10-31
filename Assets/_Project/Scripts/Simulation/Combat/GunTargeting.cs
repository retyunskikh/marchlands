using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;

namespace Marchlands.Simulation.Combat
{
    /// <summary>По кому пушка бьёт или будет бить — только чтение (для подсветки цели выбранной пушки).</summary>
    public static class GunTargeting
    {
        /// <summary>
        /// Цель пушки: на кого она уже наводится, иначе — кого выберет следующей (указанную игроком или ближайшую).
        /// false — бить не по кому.
        /// </summary>
        public static bool TryGetTarget(Match match, Squad gun, out Squad squad, out Building building)
        {
            squad = null;
            building = null;
            if (gun == null || !gun.IsArtillery)
            {
                return false;
            }

            if (gun.AimTargetId > 0)
            {
                squad = match.FindSquad(gun.AimTargetId);
                if (squad != null)
                {
                    return true;
                }
            }
            else if (gun.AimTargetId < 0)
            {
                building = match.FindBuilding(-gun.AimTargetId);
                if (building != null)
                {
                    return true;
                }
            }

            return ArtillerySystem.FindTarget(match, gun, out squad, out building);
        }
    }
}
