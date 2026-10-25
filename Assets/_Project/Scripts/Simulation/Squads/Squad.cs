using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Core;
using Unity.Mathematics;

namespace Marchlands.Simulation.Squads
{
    /// <summary>
    /// Отряд — единица управления. Снаружи сборки доступен только для чтения: меняют его системы симуляции
    /// и команды. Солдат в симуляции нет — только их число.
    /// </summary>
    public sealed class Squad
    {
        internal Squad(int id, Side side, int alive, float2 position, float facing, Formation formation)
        {
            Id = id;
            Side = side;
            Alive = alive;
            Position = position;
            PreviousPosition = position;
            Facing = Angles.Normalize(facing);
            PreviousFacing = Facing;
            Formation = formation;
            TargetFormation = formation;
            Order = SquadOrder.None;
        }

        public int Id { get; }

        public Side Side { get; }

        public int Alive { get; internal set; }

        /// <summary>Центр габаритов отряда.</summary>
        public float2 Position { get; internal set; }

        /// <summary>Направление фронта в градусах (см. <see cref="Angles"/>).</summary>
        public float Facing { get; internal set; }

        /// <summary>Позиция в начале последнего тика — для интерполяции в отображении.</summary>
        public float2 PreviousPosition { get; internal set; }

        /// <summary>Фронт в начале последнего тика — для интерполяции в отображении.</summary>
        public float PreviousFacing { get; internal set; }

        public Formation Formation { get; internal set; }

        /// <summary>Строй, в который отряд перестраивается; совпадает с <see cref="Formation"/>, если перестроения нет.</summary>
        public Formation TargetFormation { get; internal set; }

        public float FormationChangeRemaining { get; internal set; }

        /// <summary>Доля выполненного перестроения, в [0, 1).</summary>
        public float FormationChangeProgress { get; internal set; }

        public bool IsChangingFormation => FormationChangeRemaining > 0f;

        public SquadOrder Order { get; internal set; }

        public FireState FireState { get; internal set; } = FireState.Loaded;

        /// <summary>Сколько осталось до конца прицеливания или перезарядки, s.</summary>
        public float FireTimer { get; internal set; }

        /// <summary>Отряд ещё не стрелял в этом бою — следующий залп получит бонус первого.</summary>
        public bool IsFirstVolley { get; internal set; } = true;

        /// <summary>Залп сам, как только цель войдёт в эффективную дальность.</summary>
        public bool FireAtWill { get; internal set; }

        /// <summary>Цель, выбранная в начале прицеливания; 0 — нет.</summary>
        public int AimTargetId { get; internal set; }
    }
}
