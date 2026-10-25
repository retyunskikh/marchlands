using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Unity.Mathematics;

namespace Marchlands.Simulation.Buildings
{
    /// <summary>Правила постройки и найма. По ним включаются кнопки и подсвечивается силуэт казармы.</summary>
    public static class BuildRules
    {
        public static bool CanAffordBarracks(Match match, Side side)
        {
            return match.GetPlayer(side).Gold >= match.Settings.buildings.barracksCost;
        }

        /// <summary>Подходит ли место: в радиусе своего штаба и не ближе buildingSpacing к зданиям и деревням.</summary>
        public static bool IsValidBarracksSite(Match match, Side side, float2 point)
        {
            BuildingSettings settings = match.Settings.buildings;
            Building headquarters = match.GetHeadquarters(side);
            if (headquarters == null || math.distance(point, headquarters.Position) > settings.buildRadiusFromHQ)
            {
                return false;
            }

            foreach (Building building in match.Buildings)
            {
                if (math.distance(point, building.Position) < settings.buildingSpacing)
                {
                    return false;
                }
            }

            foreach (Village village in match.Villages)
            {
                if (math.distance(point, village.Position) < settings.buildingSpacing)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool CanBuildBarracks(Match match, Side side, float2 point)
        {
            return CanAffordBarracks(match, side) && IsValidBarracksSite(match, side, point);
        }

        public static bool CanRecruit(Match match, Building building)
        {
            if (building == null
                || building.Type != BuildingType.Barracks
                || !building.IsBuilt
                || building.RecruitQueue >= match.Settings.buildings.recruitQueueMax)
            {
                return false;
            }

            return RecruitsFromReserve(match, building.Owner)
                || match.GetPlayer(building.Owner).Gold >= match.Settings.buildings.squadCost;
        }

        /// <summary>Набрался ли в резерве полный отряд: тогда найм бесплатный и берёт людей из резерва.</summary>
        public static bool RecruitsFromReserve(Match match, Side side)
        {
            return match.GetPlayer(side).Reserve >= match.Settings.squads.squadSize;
        }
    }
}
