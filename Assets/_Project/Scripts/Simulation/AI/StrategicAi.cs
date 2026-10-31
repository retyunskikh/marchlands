using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.AI
{
    /// <summary>
    /// Стратегический ИИ одной стороны (этап 6 docs/design.md): в начале строит казарму в радиусе штаба на стороне,
    /// обращённой к противнику, затем держит в казармах найм и формирует отряды пехоты поровну: первыми тех, кого
    /// на поле меньше (при равенстве — фузилёров), а если их не собрать — других. Когда золота хватает,
    /// строит конюшню и держит cavalrySquads отрядов конницы; когда мест остаётся меньше houseMargin — пристраивает
    /// дом. Отрядами управляет <see cref="TacticalAi"/>. Приказы — только командами, с той же задержкой реакции.
    /// </summary>
    public sealed class StrategicAi : IMatchController
    {
        // Где пробовать ставить казарму: доли радиуса постройки и повороты от направления на врага.
        private static readonly float[] SiteDistanceFractions = { 0.6f, 0.9f, 0.35f };
        private static readonly float[] SiteAngleOffsets = { 0f, 30f, -30f, 60f, -60f, 90f, -90f };

        private static readonly UnitType[] HiredTypes = { UnitType.Fusiliers, UnitType.Grenadiers, UnitType.Cavalry };

        private int nextThinkTick;
        private int busyUntilTick = -1;

        public StrategicAi(Side side)
        {
            Side = side;
        }

        public Side Side { get; }

        public void Update(Match match)
        {
            if (match.CurrentTick < nextThinkTick)
            {
                return;
            }

            AiSettings ai = match.Settings.ai;
            nextThinkTick = match.CurrentTick + math.max(1, SecondsToTicks(ai.thinkInterval));
            if (match.CurrentTick <= busyUntilTick)
            {
                return;
            }

            Building headquarters = match.GetHeadquarters(Side);
            if (headquarters == null)
            {
                return;
            }

            if (!HasBuilding(match, BuildingType.Barracks))
            {
                if (BuildRules.CanAffordBarracks(match, Side) && TryFindBarracksSite(match, headquarters, out float2 site))
                {
                    Issue(match, new BuildBarracksCommand(Side, site));
                }

                return;
            }

            // Мест почти нет — первым делом дом, иначе найм встанет.
            int freePlaces = BuildRules.GetPopulationCapacity(match, Side) - BuildRules.GetPopulation(match, Side);
            if (freePlaces < ai.houseMargin && BuildRules.CanBuildHouse(match, Side))
            {
                Issue(match, new BuildHouseCommand(Side));
                return;
            }

            // Конюшню — когда золота хватает и на неё, и на отряд гренадёров: пехота важнее.
            int reserveGold = match.Settings.grenadiers.squadSize * match.Settings.buildings.hireCost;
            if (ai.cavalrySquads > 0 && !HasBuilding(match, BuildingType.Stable)
                && match.GetPlayer(Side).Gold >= match.Settings.buildings.stableCost + reserveGold
                && TryFindBarracksSite(match, headquarters, out float2 stableSite))
            {
                Issue(match, new BuildStableCommand(Side, stableSite));
                return;
            }

            // Артдепо — так же: когда золота хватает и на него, и на отряд гренадёров.
            if (ai.artilleryGuns > 0 && !HasBuilding(match, BuildingType.ArtilleryDepot)
                && match.GetPlayer(Side).Gold >= match.Settings.buildings.depotCost + reserveGold
                && TryFindBarracksSite(match, headquarters, out float2 depotSite))
            {
                Issue(match, new BuildDepotCommand(Side, depotSite));
                return;
            }

            int pendingGuns = 0;
            foreach (Building building in match.Buildings)
            {
                if (building.Owner != Side)
                {
                    continue;
                }

                foreach (UnitType hired in HiredTypes)
                {
                    if (BuildRules.CanHireType(building, hired) && !building.IsHiring(hired))
                    {
                        Issue(match, new SetHiringCommand(building.Id, hired, true));
                        return;
                    }
                }

                // Пушки заказывают поштучно, пока их меньше нужного.
                if (building.IsOrderingGun)
                {
                    pendingGuns += 1 + building.GunOrdersQueued;
                }
                else if (CountSquads(match, UnitType.Artillery) + pendingGuns < ai.artilleryGuns && BuildRules.CanOrderGun(match, building))
                {
                    Issue(match, new OrderGunCommand(building.Id));
                    return;
                }
            }

            UnitType first = CountSquads(match, UnitType.Grenadiers) < CountSquads(match, UnitType.Fusiliers) ? UnitType.Grenadiers : UnitType.Fusiliers;
            UnitType second = first == UnitType.Grenadiers ? UnitType.Fusiliers : UnitType.Grenadiers;
            foreach (UnitType type in new[] { first, second })
            {
                if (BuildRules.CanFormSquad(match, Side, type))
                {
                    Issue(match, new FormSquadCommand(Side, type));
                    return;
                }
            }

            if (CountSquads(match, UnitType.Cavalry) < ai.cavalrySquads && BuildRules.CanFormSquad(match, Side, UnitType.Cavalry))
            {
                Issue(match, new FormSquadCommand(Side, UnitType.Cavalry));
                return;
            }

        }

        private int CountSquads(Match match, UnitType type)
        {
            int count = 0;
            foreach (Squad squad in match.Squads)
            {
                if (squad.Side == Side && squad.Type == type && !squad.IsRouting)
                {
                    count++;
                }
            }

            return count;
        }

        private bool HasBuilding(Match match, BuildingType type)
        {
            foreach (Building building in match.Buildings)
            {
                if (building.Owner == Side && building.Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryFindBarracksSite(Match match, Building headquarters, out float2 site)
        {
            float front = Angles.Of(GetFrontDirection(match, headquarters));
            float radius = match.Settings.buildings.buildRadiusFromHQ;
            foreach (float fraction in SiteDistanceFractions)
            {
                foreach (float offset in SiteAngleOffsets)
                {
                    site = headquarters.Position + Angles.Direction(front + offset) * (radius * fraction);
                    if (BuildRules.IsValidBarracksSite(match, Side, site))
                    {
                        return true;
                    }
                }
            }

            site = float2.zero;
            return false;
        }

        private float2 GetFrontDirection(Match match, Building headquarters)
        {
            Side enemy = Side == Side.France ? Side.Austria : Side.France;
            Building enemyHeadquarters = match.GetHeadquarters(enemy);
            if (enemyHeadquarters != null && math.distancesq(enemyHeadquarters.Position, headquarters.Position) > 0f)
            {
                return math.normalize(enemyHeadquarters.Position - headquarters.Position);
            }

            return Angles.Direction(Side == Side.France ? 90f : -90f);
        }

        private void Issue(Match match, ICommand command)
        {
            int executeAt = match.CurrentTick + SecondsToTicks(match.Settings.ai.reactionDelay);
            match.Enqueue(executeAt, command);
            busyUntilTick = executeAt;
        }

        private static int SecondsToTicks(float seconds)
        {
            return (int)math.round(seconds * SimulationTime.TicksPerSecond);
        }
    }
}
