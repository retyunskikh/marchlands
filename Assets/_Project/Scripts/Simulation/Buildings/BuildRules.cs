using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Buildings
{
    /// <summary>Правила постройки, найма и формирования отрядов. По ним включаются кнопки и подсвечивается силуэт здания.</summary>
    public static class BuildRules
    {
        public static int GetCost(Match match, BuildingType type)
        {
            return type == BuildingType.Stable ? match.Settings.buildings.stableCost : match.Settings.buildings.barracksCost;
        }

        public static bool CanAfford(Match match, Side side, BuildingType type)
        {
            return match.GetPlayer(side).Gold >= GetCost(match, type);
        }

        public static bool CanAffordBarracks(Match match, Side side)
        {
            return CanAfford(match, side, BuildingType.Barracks);
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

        /// <summary>Казарму и конюшню ставят по одним правилам: в радиусе штаба и не впритык к другим.</summary>
        public static bool CanBuild(Match match, Side side, BuildingType type, float2 point)
        {
            return type != BuildingType.Headquarters && CanAfford(match, side, type) && IsValidBarracksSite(match, side, point);
        }

        public static bool CanBuildBarracks(Match match, Side side, float2 point)
        {
            return CanBuild(match, side, BuildingType.Barracks, point);
        }

        /// <summary>Найм включается в построенной казарме или конюшне.</summary>
        public static bool CanHire(Building building)
        {
            return building != null && building.Type != BuildingType.Headquarters && building.IsBuilt;
        }

        /// <summary>Где формируется отряд рода войск: конница — у конюшни, пехота — у казармы.</summary>
        public static BuildingType GetHomeType(UnitType type)
        {
            return type == UnitType.Cavalry ? BuildingType.Stable : BuildingType.Barracks;
        }

        /// <summary>Здание, у которого встаёт новый отряд: первое построенное здание нужного типа; null — нет ни одного.</summary>
        public static Building GetFormingBuilding(Match match, Side side, UnitType type)
        {
            BuildingType home = GetHomeType(type);
            foreach (Building building in match.Buildings)
            {
                if (building.Owner == side && building.Type == home && building.IsBuilt)
                {
                    return building;
                }
            }

            return null;
        }

        /// <summary>Первая построенная казарма стороны; null — нет ни одной.</summary>
        public static Building GetFormingBarracks(Match match, Side side)
        {
            return GetFormingBuilding(match, side, UnitType.Grenadiers);
        }

        /// <summary>Ждут ли люди на отряд этого рода войск и есть ли где его построить.</summary>
        public static bool CanFormSquad(Match match, Side side, UnitType type)
        {
            return match.GetPlayer(side).GetReserve(type) >= match.Settings.Unit(type).squadSize
                && GetFormingBuilding(match, side, type) != null;
        }

        /// <summary>Нанимает ли здание этот род войск: казарма — пехоту, конюшня — конницу; только построенное.</summary>
        public static bool CanHireType(Building building, UnitType type)
        {
            return CanHire(building) && building.Type == GetHomeType(type);
        }

        public static int GetHireCost(Building building, BuildingSettings settings)
        {
            return building.Type == BuildingType.Stable ? settings.cavalryHireCost : settings.hireCost;
        }

        /// <summary>Сколько людей рода войск может ждать: на maxWaitingSquads отрядов.</summary>
        public static int GetMaxWaiting(Match match, UnitType type)
        {
            return match.Settings.buildings.maxWaitingSquads * match.Settings.Unit(type).squadSize;
        }

        /// <summary>Кого нанять следующим: включённый род войск, у которого ещё есть место, начиная с очередного; null — некого.</summary>
        internal static UnitType? NextHireType(Match match, Building building)
        {
            PlayerState player = match.GetPlayer(building.Owner);
            for (int i = 0; i < 3; i++)
            {
                var type = (UnitType)((building.NextHireType + i) % 3);
                if (building.IsHiring(type) && player.GetReserve(type) < GetMaxWaiting(match, type))
                {
                    return type;
                }
            }

            return null;
        }

        public static float GetHireInterval(Building building, BuildingSettings settings)
        {
            return building.Type == BuildingType.Stable ? settings.cavalryHireInterval : settings.hireInterval;
        }

        /// <summary>Почему найм в здании сейчас не идёт; None — идёт.</summary>
        public static HireWait IsWaiting(Match match, Building building)
        {
            if (!building.IsHiringAny)
            {
                return HireWait.Off;
            }

            if (!NextHireType(match, building).HasValue)
            {
                return HireWait.Full;
            }

            return match.GetPlayer(building.Owner).Gold < GetHireCost(building, match.Settings.buildings) ? HireWait.NoGold : HireWait.None;
        }
    }
}
