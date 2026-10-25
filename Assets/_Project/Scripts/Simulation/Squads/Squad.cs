using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Morale;
using Unity.Mathematics;

namespace Marchlands.Simulation.Squads
{
    /// <summary>
    /// Отряд — единица управления. Снаружи сборки доступен только для чтения: меняют его системы симуляции
    /// и команды. Солдат в симуляции нет — только их число.
    /// </summary>
    public sealed class Squad
    {
        internal Squad(int id, Side side, UnitType type, int alive, float2 position, float facing, Formation formation)
        {
            Id = id;
            Side = side;
            Type = type;
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

        public UnitType Type { get; }

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

        /// <summary>Дальше этого залп текущего прицеливания не пойдёт (ИИ держит огонь до эффективной дальности), u.</summary>
        internal float AimMaxDistance { get; set; } = float.MaxValue;

        /// <summary>Боевой дух, 0–100.</summary>
        public float Morale { get; internal set; } = MoraleMath.MaxMorale;

        public MoraleState MoraleState { get; internal set; } = MoraleState.Steady;

        public bool IsRouting => MoraleState == MoraleState.Routing;

        /// <summary>Сколько секунд отряд не нёс потерь.</summary>
        public float TimeSinceLoss { get; internal set; }

        /// <summary>Проверка морали защитника на текущей штыковой атаке уже прошла.</summary>
        public bool ChargeChecked { get; internal set; }

        /// <summary>Сколько секунд длится текущая штыковая атака.</summary>
        internal float ChargeTime { get; set; }

        /// <summary>Со сколькими врагами отряд сейчас в рукопашной.</summary>
        public int MeleeOpponents { get; internal set; }

        public bool IsInMelee => MeleeOpponents > 0;

        /// <summary>Накопленные дробные потери в рукопашной.</summary>
        internal float MeleeLossAccumulator { get; set; }

        /// <summary>Сколько секунд отряд стоит в своей деревне с момента последнего пополнения.</summary>
        internal float ReinforceTimer { get; set; }

        /// <summary>Бегущий отряд добежал до своих и ушёл в резерв; будет удалён в конце тика.</summary>
        internal bool IsWithdrawn { get; set; }

        /// <summary>Выполняет ли отряд приказы: не бежит и не связан рукопашной.</summary>
        public bool CanReceiveOrders => !IsRouting && !IsInMelee;
    }
}
