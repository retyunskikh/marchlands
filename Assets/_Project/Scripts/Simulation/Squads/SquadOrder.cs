using Unity.Mathematics;

namespace Marchlands.Simulation.Squads
{
    public enum SquadOrderType
    {
        None,
        Move,
        Charge,

        /// <summary>В штыки на вражеское здание.</summary>
        Assault
    }

    /// <summary>Текущий приказ отряда.</summary>
    public readonly struct SquadOrder
    {
        public static readonly SquadOrder None = default;

        private SquadOrder(SquadOrderType type, float2 target, float finalFacing, int targetSquadId, int targetBuildingId = 0)
        {
            Type = type;
            Target = target;
            FinalFacing = finalFacing;
            TargetSquadId = targetSquadId;
            TargetBuildingId = targetBuildingId;
        }

        public SquadOrderType Type { get; }

        /// <summary>Точка назначения приказа движения.</summary>
        public float2 Target { get; }

        /// <summary>Направление фронта после прибытия, в градусах.</summary>
        public float FinalFacing { get; }

        /// <summary>Атакуемый отряд для штыковой атаки.</summary>
        public int TargetSquadId { get; }

        /// <summary>Штурмуемое здание.</summary>
        public int TargetBuildingId { get; }

        public static SquadOrder Move(float2 target, float finalFacing)
        {
            return new SquadOrder(SquadOrderType.Move, target, finalFacing, 0);
        }

        public static SquadOrder Charge(int targetSquadId)
        {
            return new SquadOrder(SquadOrderType.Charge, float2.zero, 0f, targetSquadId);
        }

        public static SquadOrder Assault(int targetBuildingId)
        {
            return new SquadOrder(SquadOrderType.Assault, float2.zero, 0f, 0, targetBuildingId);
        }
    }
}
