using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Victory
{
    /// <summary>
    /// Условия победы (раздел 3.7 docs/design.md): разрушение вражеского штаба, доминирование по деревням,
    /// уничтожение армии противника. Проверяются в конце тика, если включены для матча.
    /// </summary>
    internal static class VictorySystem
    {
        // Таймеры складываются из шагов 0.1 с во float; допуск не даёт лишнего тика из-за ошибки округления.
        private const float TimerEpsilon = 1e-4f;

        private static readonly Side[] Sides = { Side.France, Side.Austria };

        public static void Update(Match match, float deltaTime)
        {
            if (!match.VictoryEnabled || match.Result != null)
            {
                return;
            }

            foreach (Side side in Sides)
            {
                CheckHeadquarters(match, side);
                if (match.Result != null)
                {
                    return;
                }
            }

            foreach (Side side in Sides)
            {
                if (UpdateDomination(match, side, deltaTime))
                {
                    match.Finish(side, VictoryReason.Domination);
                    return;
                }
            }

            foreach (Side side in Sides)
            {
                if (IsAnnihilated(match, side))
                {
                    match.Finish(Enemy(side), VictoryReason.Annihilation);
                    return;
                }
            }
        }

        /// <summary>Штаб стороны разрушен штурмом — победа противника.</summary>
        private static void CheckHeadquarters(Match match, Side owner)
        {
            Building headquarters = match.GetHeadquarters(owner);
            if (headquarters != null && headquarters.IsDestroyed)
            {
                match.Finish(Enemy(owner), VictoryReason.HeadquartersDestroyed);
            }
        }

        /// <summary>Держит ли сторона долю деревень непрерывно dominationTime секунд.</summary>
        private static bool UpdateDomination(Match match, Side side, float deltaTime)
        {
            int required = RequiredVillages(match);
            if (required <= 0)
            {
                return false;
            }

            if (match.CountVillages(side) < required)
            {
                match.SetDominationTime(side, 0f);
                return false;
            }

            float time = match.GetDominationTime(side) + deltaTime;
            match.SetDominationTime(side, time);
            return time >= match.Settings.victory.dominationTime - TimerEpsilon;
        }

        public static int RequiredVillages(Match match)
        {
            if (match.Villages.Count == 0)
            {
                return 0;
            }

            // Допуск, чтобы 2/3 × 6 не превратилось в 5 из-за округления float.
            return (int)math.ceil(match.Villages.Count * match.Settings.victory.dominationShare - TimerEpsilon);
        }

        /// <summary>Нет отрядов на поле и нельзя нанять новый.</summary>
        private static bool IsAnnihilated(Match match, Side side)
        {
            foreach (Squad squad in match.Squads)
            {
                if (squad.Side == side && !squad.IsWithdrawn && !squad.IsCamp)
                {
                    return false;
                }
            }

            return !CanStillRecruit(match, side);
        }

        /// <summary>
        /// Есть казарма (хотя бы строящаяся), а в резерве гренадёров или фузилёров вместе с теми, кого можно нанять,
        /// хватит на отряд этого рода войск, или то же для конюшни и конницы.
        /// </summary>
        private static bool CanStillRecruit(Match match, Side side)
        {
            bool hasBarracks = false;
            bool hasStable = false;
            bool hasDepot = false;
            foreach (Building building in match.Buildings)
            {
                if (building.Owner != side)
                {
                    continue;
                }

                hasBarracks |= building.Type == BuildingType.Barracks;
                hasStable |= building.Type == BuildingType.Stable;
                hasDepot |= building.Type == BuildingType.ArtilleryDepot;
            }

            PlayerState player = match.GetPlayer(side);
            BuildingSettings settings = match.Settings.buildings;
            return hasBarracks && CanGather(player.GrenadierReserve, player.Gold, settings.grenadierHireCost, match.Settings.grenadiers.squadSize)
                || hasBarracks && CanGather(player.FusilierReserve, player.Gold, settings.hireCost, match.Settings.fusiliers.squadSize)
                || hasStable && CanGather(player.CavalryReserve, player.Gold, settings.cavalryHireCost, match.Settings.cavalry.squadSize)
                || hasDepot && CanGather(player.ArtilleryReserve, player.Gold, settings.gunnerHireCost, match.Settings.artillery.squadSize);
        }

        private static bool CanGather(int reserve, int gold, int hireCost, int squadSize)
        {
            long hireable = hireCost > 0 ? gold / hireCost : int.MaxValue;
            return reserve + hireable >= squadSize;
        }

        private static Side Enemy(Side side)
        {
            return side == Side.France ? Side.Austria : Side.France;
        }
    }
}
