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

        private readonly bool[] hiring = new bool[3];

        /// <summary>Включён ли найм этого рода войск.</summary>
        public bool IsHiring(UnitType type)
        {
            return hiring[(int)type];
        }

        /// <summary>Включён ли найм хоть одного рода войск.</summary>
        public bool IsHiringAny => hiring[0] || hiring[1] || hiring[2];

        internal void SetHiring(UnitType type, bool value)
        {
            hiring[(int)type] = value;
        }

        /// <summary>С какого рода войск начинать поиск следующего рекрута — так включённые роды нанимаются по очереди.</summary>
        internal int NextHireType { get; set; }

        /// <summary>Сколько осталось до прихода следующего рекрута, s.</summary>
        public float HireRemaining { get; internal set; }

        /// <summary>Прогресс захвата штаба врагом, s (до hqCaptureTime).</summary>
        public float CaptureProgress { get; internal set; }
    }
}
