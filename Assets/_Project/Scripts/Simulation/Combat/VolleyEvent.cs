using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Combat
{
    /// <summary>
    /// Запись о залпе для отображения: кто стрелял, по кому и с каким итогом. Положение цели сохранено
    /// на момент залпа — цель могла погибнуть и исчезнуть из матча.
    /// </summary>
    public readonly struct VolleyEvent
    {
        public VolleyEvent(
            int tick, int shooterId, Side shooterSide, int shooters, float distance, int casualties,
            int targetId, int targetAliveBefore, float2 targetPosition, float targetFacing, Formation targetFormation)
        {
            Tick = tick;
            ShooterId = shooterId;
            ShooterSide = shooterSide;
            Shooters = shooters;
            Distance = distance;
            Casualties = casualties;
            TargetId = targetId;
            TargetAliveBefore = targetAliveBefore;
            TargetPosition = targetPosition;
            TargetFacing = targetFacing;
            TargetFormation = targetFormation;
        }

        public int Tick { get; }

        public int ShooterId { get; }

        public Side ShooterSide { get; }

        public int Shooters { get; }

        public float Distance { get; }

        public int Casualties { get; }

        public int TargetId { get; }

        public int TargetAliveBefore { get; }

        public float2 TargetPosition { get; }

        public float TargetFacing { get; }

        public Formation TargetFormation { get; }
    }
}
