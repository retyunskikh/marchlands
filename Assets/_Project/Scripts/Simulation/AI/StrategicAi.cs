using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Unity.Mathematics;

namespace Marchlands.Simulation.AI
{
    /// <summary>
    /// Стратегический ИИ одной стороны (этап 6 docs/design.md): в начале строит казарму в радиусе штаба на стороне,
    /// обращённой к противнику, затем нанимает, когда хватает золота. Отрядами управляет <see cref="TacticalAi"/>.
    /// Приказы — только командами, с той же задержкой реакции.
    /// </summary>
    public sealed class StrategicAi : IMatchController
    {
        // Где пробовать ставить казарму: доли радиуса постройки и повороты от направления на врага.
        private static readonly float[] SiteDistanceFractions = { 0.6f, 0.9f, 0.35f };
        private static readonly float[] SiteAngleOffsets = { 0f, 30f, -30f, 60f, -60f, 90f, -90f };

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

            if (!HasBarracks(match))
            {
                if (BuildRules.CanAffordBarracks(match, Side) && TryFindBarracksSite(match, headquarters, out float2 site))
                {
                    Issue(match, new BuildBarracksCommand(Side, site));
                }

                return;
            }

            foreach (Building building in match.Buildings)
            {
                if (building.Owner == Side && BuildRules.CanRecruit(match, building))
                {
                    Issue(match, new RecruitCommand(building.Id));
                    return;
                }
            }
        }

        private bool HasBarracks(Match match)
        {
            foreach (Building building in match.Buildings)
            {
                if (building.Owner == Side && building.Type == BuildingType.Barracks)
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
