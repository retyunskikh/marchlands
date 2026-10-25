using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Buildings
{
    public enum BuildingType
    {
        Headquarters,
        Barracks,
        Stable
    }

    /// <summary>Здание стороны: штаб или казарма. Снаружи сборки только для чтения.</summary>
    public sealed class Building
    {
        internal Building(int id, BuildingType type, Side owner, float2 position, float buildTime)
        {
            Id = id;
            Type = type;
            Owner = owner;
            Position = position;
            BuildTime = buildTime;
            BuildRemaining = buildTime;
        }

        public int Id { get; }

        public BuildingType Type { get; }

        public Side Owner { get; }

        public float2 Position { get; }

        /// <summary>Полное время постройки, s.</summary>
        public float BuildTime { get; }

        /// <summary>Сколько осталось строить, s.</summary>
        public float BuildRemaining { get; internal set; }

        public bool IsBuilt => BuildRemaining <= 0f;

        /// <summary>Доля выполненной постройки, в [0, 1].</summary>
        public float BuildProgress => BuildTime > 0f ? math.saturate(1f - BuildRemaining / BuildTime) : 1f;

        /// <summary>Идёт ли в казарме или конюшне найм.</summary>
        public bool IsHiring { get; internal set; }

        /// <summary>Найм приостанавливается, когда людей хватает на отряд этого рода войск; null — найм без остановки.</summary>
        public UnitType? HireUntil { get; internal set; }

        /// <summary>Сколько осталось до прихода следующего рекрута, s.</summary>
        public float HireRemaining { get; internal set; }

        /// <summary>Прогресс захвата штаба врагом, s (до hqCaptureTime).</summary>
        public float CaptureProgress { get; internal set; }
    }
}
