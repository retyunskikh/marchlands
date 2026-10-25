using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Buildings
{
    /// <summary>Правила постройки, найма и формирования отрядов. По ним включаются кнопки и подсвечивается силуэт казармы.</summary>
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

        /// <summary>Найм включается в построенной казарме.</summary>
        public static bool CanHire(Building building)
        {
            return building != null && building.Type == BuildingType.Barracks && building.IsBuilt;
        }

        /// <summary>Казарма, у которой встаёт новый отряд: первая построенная казарма стороны; null — нет ни одной.</summary>
        public static Building GetFormingBarracks(Match match, Side side)
        {
            foreach (Building building in match.Buildings)
            {
                if (building.Owner == side && building.Type == BuildingType.Barracks && building.IsBuilt)
                {
                    return building;
                }
            }

            return null;
        }

        /// <summary>Ждут ли у казарм люди на отряд этого рода войск и есть ли где его построить.</summary>
        public static bool CanFormSquad(Match match, Side side, UnitType type)
        {
            return match.GetPlayer(side).Reserve >= match.Settings.Unit(type).squadSize
                && GetFormingBarracks(match, side) != null;
        }
    }
}
