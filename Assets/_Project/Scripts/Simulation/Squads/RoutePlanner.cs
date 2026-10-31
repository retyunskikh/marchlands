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
            Formation marching = squad.Type == UnitType.Cavalry || squad.IsArtillery ? squad.Formation : Formation.Column;
            float marchSpeed = math.max(0.01f, UnitStats.Speed(squad, marching, match.Settings));
            bool reform = squad.Formation != marching;
            // Перестроение в колонну на ходу идёт медленнее: пока строятся — скорость прежнего строя × reformMoveSpeedMultiplier.
            float reformLoss = reform ? settings.formationChangeTime * (1f - settings.reformMoveSpeedMultiplier) : 0f;
            float routeTime = routeCost / marchSpeed + reformLoss;
            float straightCost = pathfinder.SegmentCost(squad.Position, target);
            float straightTime = straightCost / marchSpeed;
            if (!float.IsInfinity(straightCost) && routeTime >= straightTime * settings.autoRouteGain)
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
