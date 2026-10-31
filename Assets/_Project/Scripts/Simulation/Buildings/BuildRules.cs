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
            switch (type)
            {
                case BuildingType.Stable:
                    return match.Settings.buildings.stableCost;
                case BuildingType.ArtilleryDepot:
                    return match.Settings.buildings.depotCost;
                default:
                    return match.Settings.buildings.barracksCost;
            }
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
                float spacing = settings.buildingSpacing + (building.Type == BuildingType.Headquarters ? settings.annexSpacing : 0f);
                if (math.distance(point, building.Position) < spacing)
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
            return building != null && building.IsBuilt
                && (building.Type == BuildingType.Barracks || building.Type == BuildingType.Stable || building.Type == BuildingType.ArtilleryDepot);
        }

        /// <summary>Где формируется отряд рода войск: конница — у конюшни, пушки — у артдепо, пехота — у казармы.</summary>
        public static BuildingType GetHomeType(UnitType type)
        {
            switch (type)
            {
                case UnitType.Cavalry:
                    return BuildingType.Stable;
                case UnitType.Artillery:
                    return BuildingType.ArtilleryDepot;
                default:
                    return BuildingType.Barracks;
            }
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
            // Пушки не нанимают постоянно — их заказывают поштучно (см. OrderGunCommand).
            return type != UnitType.Artillery && CanHire(building) && building.Type == GetHomeType(type);
        }

        /// <summary>
        /// Цена следующей пушки стороны: gunCrewHireCost × gunCostGrowth^n, где n — её пушки в строю и заказанные.
        /// Погибла или брошена пушка — следующая снова дешевле.
        /// </summary>
        public static int GetGunCost(Match match, Side side)
        {
            int guns = 0;
            foreach (Squad squad in match.Squads)
            {
                if (squad.Side == side && squad.IsArtillery && !squad.IsRouting && !squad.IsWithdrawn && squad.Alive > 0)
                {
                    guns++;
                }
            }

            foreach (Building building in match.Buildings)
            {
                if (building.Owner == side && building.IsOrderingGun)
                {
                    guns += 1 + building.GunOrdersQueued;
                }
            }

            BuildingSettings settings = match.Settings.buildings;
            return (int)math.round(settings.gunCrewHireCost * math.pow(math.max(1f, settings.gunCostGrowth), guns));
        }

        /// <summary>
        /// Можно ли заказать пушку в этом депо: построено, есть место в очереди (одна готовится и до gunQueueSize ждут),
        /// хватает золота и мест на расчёт.
        /// </summary>
        public static bool CanOrderGun(Match match, Building depot)
        {
            return depot != null && depot.Type == BuildingType.ArtilleryDepot && depot.IsBuilt && !depot.IsDestroyed
                && (!depot.IsOrderingGun || depot.GunOrdersQueued < match.Settings.buildings.gunQueueSize)
                && HasHousing(match, depot.Owner, match.Settings.artillery.squadSize)
                && match.GetPlayer(depot.Owner).Gold >= GetGunCost(match, depot.Owner);
        }

        /// <summary>Цена одного рекрута рода войск.</summary>
        public static int GetHireCost(BuildingSettings settings, UnitType type)
        {
            switch (type)
            {
                case UnitType.Cavalry:
                    return settings.cavalryHireCost;
                case UnitType.Artillery:
                    return settings.gunCrewHireCost;
                case UnitType.Grenadiers:
                    return settings.grenadierHireCost;
                default:
                    return settings.hireCost;
            }
        }

        /// <summary>Раз в столько секунд здание получает рекрута рода войск, s.</summary>
        public static float GetHireInterval(BuildingSettings settings, UnitType type)
        {
            switch (type)
            {
                case UnitType.Cavalry:
                    return settings.cavalryHireInterval;
                case UnitType.Artillery:
                    return settings.gunCrewHireInterval;
                case UnitType.Grenadiers:
                    return settings.grenadierHireInterval;
                default:
                    return settings.hireInterval;
            }
        }

        /// <summary>
        /// Кого здание нанимает сейчас — по нему цена и таймер: очередной включённый род войск с местом в резерве;
        /// если места нет ни у кого — первый включённый; если найм выключен — основной род войск здания.
        /// </summary>
        public static UnitType GetUpcomingHireType(Match match, Building building)
        {
            UnitType? next = NextHireType(match, building);
            if (next.HasValue)
            {
                return next.Value;
            }

            foreach (UnitType type in HireOrder)
            {
                if (building.IsHiring(type))
                {
                    return type;
                }
            }

            return building.Type == BuildingType.Stable ? UnitType.Cavalry
                : building.Type == BuildingType.ArtilleryDepot ? UnitType.Artillery
                : UnitType.Fusiliers;
        }

        public static int GetHireCost(Match match, Building building)
        {
            return GetHireCost(match.Settings.buildings, GetUpcomingHireType(match, building));
        }

        public static float GetHireInterval(Match match, Building building)
        {
            return GetHireInterval(match.Settings.buildings, GetUpcomingHireType(match, building));
        }

        /// <summary>Сколько людей рода войск может ждать: на maxWaitingSquads отрядов.</summary>
        public static int GetMaxWaiting(Match match, UnitType type)
        {
            return match.Settings.buildings.maxWaitingSquads * match.Settings.Unit(type).squadSize;
        }

        /// <summary>Очерёдность найма: фузилёры, гренадёры, конница, пушкари.</summary>
        internal static readonly UnitType[] HireOrder = { UnitType.Fusiliers, UnitType.Grenadiers, UnitType.Cavalry, UnitType.Artillery };

        /// <summary>Кого нанять следующим: включённый род войск, у которого ещё есть место, начиная с очередного; null — некого.</summary>
        internal static UnitType? NextHireType(Match match, Building building)
        {
            PlayerState player = match.GetPlayer(building.Owner);
            for (int i = 0; i < HireOrder.Length; i++)
            {
                UnitType type = HireOrder[(building.NextHireType + i) % HireOrder.Length];
                if (building.IsHiring(type) && player.GetReserve(type) < GetMaxWaiting(match, type))
                {
                    return type;
                }
            }

            return null;
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

            if (!HasHousing(match, building.Owner))
            {
                return HireWait.NoHousing;
            }

            return match.GetPlayer(building.Owner).Gold < GetHireCost(match, building) ? HireWait.NoGold : HireWait.None;
        }

        /// <summary>Население стороны: живые солдаты её отрядов (и бегущих) плюс рекруты в резерве.</summary>
        public static int GetPopulation(Match match, Side side)
        {
            int population = 0;
            foreach (Squad squad in match.Squads)
            {
                // Толпа у казармы — это и есть резерв, его считаем ниже.
                if (squad.Side == side && !squad.IsCamp && !squad.IsWithdrawn)
                {
                    population += squad.Alive;
                }
            }

            PlayerState player = match.GetPlayer(side);
            foreach (UnitType type in HireOrder)
            {
                population += player.GetReserve(type);
            }

            return population;
        }

        /// <summary>Сколько мест у стороны: штаб плюс дома; без штаба (песочницы) — без предела.</summary>
        public static int GetPopulationCapacity(Match match, Side side)
        {
            Building headquarters = match.GetHeadquarters(side);
            BuildingSettings settings = match.Settings.buildings;
            return headquarters == null ? int.MaxValue : settings.headquartersPopulation + headquarters.Houses * settings.housePopulation;
        }

        /// <summary>Есть ли место ещё для одного человека.</summary>
        public static bool HasHousing(Match match, Side side)
        {
            return HasHousing(match, side, 1);
        }

        /// <summary>Хватит ли мест ещё на <paramref name="people"/> человек.</summary>
        public static bool HasHousing(Match match, Side side, int people)
        {
            return GetPopulation(match, side) + people <= GetPopulationCapacity(match, side);
        }

        /// <summary>Цена следующего дома: houseCost, и каждый следующий вдвое дороже.</summary>
        public static int GetHouseCost(Match match, Side side)
        {
            Building headquarters = match.GetHeadquarters(side);
            int built = headquarters != null ? headquarters.Houses : 0;
            return match.Settings.buildings.houseCost << math.min(built, 20);
        }

        /// <summary>Можно ли заложить дом: штаб есть, дом сейчас не строится, предел не достигнут, золота хватает.</summary>
        public static bool CanBuildHouse(Match match, Side side)
        {
            Building headquarters = match.GetHeadquarters(side);
            return headquarters != null
                && !headquarters.IsBuildingHouse
                && headquarters.Houses < match.Settings.buildings.maxHouses
                && match.GetPlayer(side).Gold >= GetHouseCost(match, side);
        }
    }
}
