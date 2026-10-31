using Marchlands.Simulation.Core;
using Unity.Mathematics;

namespace Marchlands.Simulation.Buildings
{
    /// <summary>Запись о залпе часовых здания для отображения: откуда стреляли, по кому и с каким итогом.</summary>
    public readonly struct SentryVolley
    {
        public SentryVolley(int buildingId, Side side, float2 buildingPosition, int shooters, int casualties, int targetId, float2 targetPosition,
            int misfires = 0)
        {
            Misfires = misfires;
            BuildingId = buildingId;
            Side = side;
            BuildingPosition = buildingPosition;
            Shooters = shooters;
            Casualties = casualties;
            TargetId = targetId;
            TargetPosition = targetPosition;
        }

        public int BuildingId { get; }

        public Side Side { get; }

        public float2 BuildingPosition { get; }

        public int Shooters { get; }

        /// <summary>Сколько ружей дали осечку в дождь; <see cref="Shooters"/> — сколько выстрелило.</summary>
        public int Misfires { get; }

        public int Casualties { get; }

        public int TargetId { get; }

        public float2 TargetPosition { get; }
    }
}
