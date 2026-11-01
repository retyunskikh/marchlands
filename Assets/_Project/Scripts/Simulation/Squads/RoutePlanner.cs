using System.Collections.Generic;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Terrain;
using Unity.Mathematics;

namespace Marchlands.Simulation.Squads
{
    /// <summary>
    /// Маршрут дальнего приказа игрока: путь по местности (A*: дорога дешевле, лес дороже, река — только по переправам).
    /// Берётся, только если он вместе с перестроением в колонну быстрее, чем идти напрямую в нынешнем строю;
    /// тогда отряд в линии перестраивается в колонну на ходу, а у цели — обратно.
    /// </summary>
    internal static class RoutePlanner
    {
        private static readonly List<float2> Waypoints = new List<float2>();

        // Шаг, с которым путь проверяется на дорогу, u.
        private const float RoadSampleStep = 1f;

        /// <summary>Какая доля длины найденного пути (<see cref="Waypoints"/>) идёт по дорогам.</summary>
        private static float RoadShare(Match match, float2 from)
        {
            float halfWidth = match.Settings.terrain.roadHalfWidth;
            float total = 0f;
            float onRoad = 0f;
            float2 previous = from;
            foreach (float2 point in Waypoints)
            {
                float length = math.distance(previous, point);
                int samples = math.max(1, (int)math.ceil(length / RoadSampleStep));
                for (int i = 0; i < samples; i++)
                {
                    if (match.Terrain.IsOnRoad(math.lerp(previous, point, (i + 0.5f) / samples), halfWidth))
                    {
                        onRoad += length / samples;
                    }
                }

                total += length;
                previous = point;
            }

            return total > 0f ? onRoad / total : 0f;
        }

        /// <summary>Есть ли ближе <paramref name="radius"/> не бегущий вражеский отряд (не толпа рекрутов).</summary>
        public static bool IsEnemyNear(Match match, Squad squad, float radius)
        {
            if (radius <= 0f)
            {
                return false;
            }

            foreach (Squad other in match.Squads)
            {
                if (other.Side != squad.Side && !other.IsRouting && !other.IsWithdrawn && !other.IsCamp && other.Alive > 0
                    && math.distancesq(other.Position, squad.Position) <= radius * radius)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Есть ли ближе <paramref name="radius"/> не бегущий враг впереди по ходу — в полуплоскости перед отрядом
        /// по направлению <paramref name="heading"/>. Враг сзади (от него уходят) не в счёт.
        /// </summary>
        public static bool IsEnemyAhead(Match match, Squad squad, float radius, float2 heading)
        {
            if (radius <= 0f)
            {
                return false;
            }

            bool anyDirection = math.lengthsq(heading) < 1e-6f;
            foreach (Squad other in match.Squads)
            {
                if (other.Side == squad.Side || other.IsRouting || other.IsWithdrawn || other.IsCamp || other.Alive <= 0)
                {
                    continue;
                }

                float2 offset = other.Position - squad.Position;
                if (math.lengthsq(offset) <= radius * radius && (anyDirection || math.dot(offset, heading) > 0f))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryPlan(Match match, Squad squad, float2 target)
        {
            SquadSettings settings = match.Settings.squads;
            TerrainPathfinder pathfinder = match.Pathfinder;
            if (pathfinder == null || settings.autoRouteMinDistance <= 0f
                || math.distance(squad.Position, target) < settings.autoRouteMinDistance)
            {
                return false;
            }

            if (!pathfinder.FindPath(squad.Position, target, Waypoints) || Waypoints.Count < 2)
            {
                return false;
            }

            // По дороге — серединой: точки пути у дороги встают на её ось, изгибы дороги — в маршрут.
            TerrainSettings terrain = match.Settings.terrain;
            match.Terrain.AlignToRoads(squad.Position, Waypoints, terrain.roadHalfWidth + terrain.pathCellSize, settings.roadAlignMaxDetour);

            float routeCost = 0f;
            float2 previous = squad.Position;
            foreach (float2 point in Waypoints)
            {
                routeCost += pathfinder.SegmentCost(previous, point);
                previous = point;
            }

            // Сравниваем путь по местности с прямым в одном и том же строю: маршрут нужен ради дороги или обхода
            // леса и реки, а не ради колонны. В чистом поле отряд идёт напрямую в своём строю.
            // Конница и пушки строй не меняют: идут по дороге тем строем, что есть.
            // Пехота на марше встаёт в колонну, только если идти далеко и враг не рядом; иначе идёт тем строем, что есть.
            bool keepsFormation = squad.Type == UnitType.Cavalry || squad.IsArtillery
                || math.distance(squad.Position, target) < settings.autoColumnMinDistance
                || IsEnemyAhead(match, squad, settings.autoLineEnemyRadius, target - squad.Position);
            Formation marching = keepsFormation ? squad.TargetFormation : Formation.Column;
            float marchSpeed = math.max(0.01f, UnitStats.Speed(squad, marching, match.Settings));
            bool reform = squad.TargetFormation != marching;
            // Перестроение в колонну на ходу идёт медленнее: пока строятся — скорость прежнего строя × reformMoveSpeedMultiplier.
            float reformLoss = reform ? settings.formationChangeTime * (1f - settings.reformMoveSpeedMultiplier) : 0f;
            float routeTime = routeCost / marchSpeed + reformLoss;
            float straightCost = pathfinder.SegmentCost(squad.Position, target);
            float straightTime = straightCost / marchSpeed;
            // Путь в основном по дорогам берётся охотнее: по дороге в колонне быстрее, чем линией по полю.
            float gain = RoadShare(match, squad.Position) >= settings.roadRouteMinShare && settings.roadRouteMinShare > 0f
                ? math.max(settings.autoRouteGain, settings.roadRouteGain)
                : settings.autoRouteGain;
            if (!float.IsInfinity(straightCost) && routeTime >= straightTime * gain)
            {
                return false;
            }

            squad.Route.AddRange(Waypoints);
            squad.RouteIndex = 0;
            if (reform)
            {
                squad.RestoreFormation = squad.Formation;
                ChangeFormationCommand.Start(match, squad, marching);
            }

            return true;
        }
    }
}
