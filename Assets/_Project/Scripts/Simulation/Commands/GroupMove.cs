using System.Collections.Generic;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Commands
{
    /// <summary>
    /// Приказ движения группе отрядов: каждый идёт со своим смещением от центра группы, и вся группа
    /// разворачивается к новому фронту как одно целое — взаимное расположение сохраняется, отряды не сбиваются в кучу.
    /// Только расчёт точек: сами приказы — обычные <see cref="MoveCommand"/>, по одному на отряд.
    /// </summary>
    public static class GroupMove
    {
        /// <summary>Центр группы — среднее положение отрядов.</summary>
        public static float2 Center(IReadOnlyList<Squad> squads)
        {
            float2 sum = float2.zero;
            foreach (Squad squad in squads)
            {
                sum += squad.Position;
            }

            return squads.Count > 0 ? sum / squads.Count : float2.zero;
        }

        /// <summary>
        /// Куда идти каждому отряду (в том же порядке): центр группы встаёт в <paramref name="destination"/>,
        /// а смещения отрядов от центра поворачиваются на угол от <paramref name="referenceFacing"/>
        /// (нынешний фронт группы) к <paramref name="facing"/> (новый фронт).
        /// </summary>
        public static void Arrange(IReadOnlyList<Squad> squads, float2 destination, float facing, float referenceFacing,
            List<float2> targets)
        {
            targets.Clear();
            float2 center = Center(squads);
            float turn = math.radians(Angles.Delta(referenceFacing, facing));
            float cos = math.cos(turn);
            float sin = math.sin(turn);
            foreach (Squad squad in squads)
            {
                float2 offset = squad.Position - center;
                float2 rotated = new float2(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos);
                targets.Add(destination + rotated);
            }
        }

        /// <summary>Отдаёт приказы движения всей группе (кто может их получить).</summary>
        public static void Enqueue(Match match, IReadOnlyList<Squad> squads, float2 destination, float facing,
            float referenceFacing, bool autoRoute)
        {
            if (squads.Count == 1)
            {
                match.Enqueue(new MoveCommand(squads[0].Id, destination, facing, autoRoute));
                return;
            }

            Arrange(squads, destination, facing, referenceFacing, Targets);
            for (int i = 0; i < squads.Count; i++)
            {
                if (squads[i].CanReceiveOrders)
                {
                    match.Enqueue(new MoveCommand(squads[i].Id, Targets[i], facing, autoRoute));
                }
            }
        }

        /// <summary>
        /// Как <see cref="Enqueue"/>, но точку задают для одного отряда группы (<paramref name="anchor"/>, от которого
        /// тянули приказ): он встаёт в неё, остальные — со своими смещениями при том же развороте.
        /// </summary>
        public static void EnqueueAnchored(Match match, IReadOnlyList<Squad> squads, Squad anchor, float2 anchorDestination,
            float facing, float referenceFacing, bool autoRoute)
        {
            float2 center = Center(squads);
            float turn = math.radians(Angles.Delta(referenceFacing, facing));
            float2 offset = anchor.Position - center;
            float2 rotated = new float2(offset.x * math.cos(turn) - offset.y * math.sin(turn),
                offset.x * math.sin(turn) + offset.y * math.cos(turn));
            Enqueue(match, squads, anchorDestination - rotated, facing, referenceFacing, autoRoute);
        }

        private static readonly List<float2> Targets = new List<float2>();
    }
}
