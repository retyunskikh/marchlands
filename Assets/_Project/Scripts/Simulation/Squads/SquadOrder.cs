using Unity.Mathematics;

namespace Marchlands.Simulation.Squads
{
    public enum SquadOrderType
    {
        None,
        Move
    }

    /// <summary>Текущий приказ отряда.</summary>
    public readonly struct SquadOrder
    {
        public static readonly SquadOrder None = default;

        private SquadOrder(SquadOrderType type, float2 target, float finalFacing)
        {
            Type = type;
            Target = target;
            FinalFacing = finalFacing;
        }

        public SquadOrderType Type { get; }

        public float2 Target { get; }

        /// <summary>Направление фронта после прибытия, в градусах.</summary>
        public float FinalFacing { get; }

        public static SquadOrder Move(float2 target, float finalFacing)
        {
            return new SquadOrder(SquadOrderType.Move, target, finalFacing);
        }
    }
}
