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

        /// <summary>Толпа рекрутов — у конюшни (резерв конницы), иначе у казармы (пехотный резерв).</summary>
        public bool IsCavalryCamp { get; internal set; }

        /// <summary>Численность толпы рекрутов после последней сверки с резервом; разница — погибшие.</summary>
        internal int SyncedAlive { get; set; }

        /// <summary>Накопленные дробные выстрелы стрельбы вразнобой.</summary>
        internal float SkirmishShots { get; set; }

        /// <summary>Выстрелы вразнобой, которые готовятся (человек целится aimTime секунд, потом стреляет).</summary>
        internal readonly System.Collections.Generic.List<SkirmishAim> SkirmishAimList = new System.Collections.Generic.List<SkirmishAim>();

        public System.Collections.Generic.IReadOnlyList<SkirmishAim> SkirmishAims => SkirmishAimList;

        /// <summary>Сколько выстрелов вразнобой отряд начал готовить — по нему выбирается следующий стрелок.</summary>
        internal int SkirmishShotCount { get; set; }

        /// <summary>
        /// Маршрут дальнего приказа движения (точки пути по местности, последняя — цель приказа); пустой — идти напрямую.
        /// </summary>
        internal readonly System.Collections.Generic.List<float2> Route = new System.Collections.Generic.List<float2>();

        /// <summary>К какой точке маршрута отряд идёт сейчас.</summary>
        internal int RouteIndex { get; set; }

        /// <summary>В какой строй вернуться, дойдя до цели (отряд перестроился в колонну для марша); null — не менять.</summary>
        internal Formation? RestoreFormation { get; set; }

        /// <summary>Точки маршрута, которые отряду ещё предстоит пройти (для отображения).</summary>
        public System.Collections.Generic.IReadOnlyList<float2> RouteWaypoints => Route;

        public int CurrentRouteIndex => RouteIndex;

        public bool HasRoute => Route.Count > 0 && RouteIndex < Route.Count;

        /// <summary>Сбрасывает маршрут и возврат строя — новый приказ отменяет марш.</summary>
        internal void ClearRoute()
        {
            Route.Clear();
            RouteIndex = 0;
            RestoreFormation = null;
            SuspendedOrder = null;
        }

        /// <summary>
        /// Отложенный приказ марша: отряд развернули на месте или втянули в рукопашную посреди марша. Когда рядом
        /// не останется врагов, марш (с маршрутом) продолжится; null — откладывать нечего.
        /// </summary>
        internal SquadOrder? SuspendedOrder { get; set; }

        /// <summary>Марш отложен и продолжится, когда рядом не будет врагов.</summary>
        public bool IsMarchSuspended => SuspendedOrder.HasValue;

        /// <summary>Куда проложен маршрут бегства (бегущий перепрокладывает его, если цель сменилась).</summary>
        internal float2 RouteGoal { get; set; }

        /// <summary>Стоит ли центр отряда на холме (обновляется после движения): стреляет дальше.</summary>
        public bool IsOnHill { get; internal set; }

        /// <summary>Толпа рекрутов, а не управляемый отряд.</summary>
        public bool IsCamp => Type == UnitType.Recruits;

        /// <summary>Сколько секунд отряд стоит в своей деревне с момента последнего пополнения.</summary>
        internal float ReinforceTimer { get; set; }

        /// <summary>Отряд в зоне своей деревни и пополняется (не в полном составе).</summary>
        public bool IsReinforcing { get; internal set; }

        /// <summary>Доля пути до следующего человека из деревни, 0..1; 0, если не пополняется.</summary>
        public float ReinforceProgress { get; internal set; }

        /// <summary>Бегущий отряд добежал до своих и ушёл в резерв; будет удалён в конце тика.</summary>
        internal bool IsWithdrawn { get; set; }

        /// <summary>Выполняет ли отряд приказы: не бежит и не связан рукопашной.</summary>
        public bool CanReceiveOrders => !IsRouting && !IsInMelee;
    }
}
