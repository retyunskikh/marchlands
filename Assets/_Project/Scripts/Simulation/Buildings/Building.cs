using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Buildings
{
    public enum BuildingType
    {
        Headquarters,
        Barracks,
        Stable,

        /// <summary>Блокгауз: стоит с начала матча у прохода к базе, отстреливается сильнее часовых, не нанимает.</summary>
        Blockhouse,

        /// <summary>Артиллерийское депо: нанимает пушкарей, из них составляются пушки.</summary>
        ArtilleryDepot
    }

    /// <summary>Здание стороны: штаб или казарма. Снаружи сборки только для чтения.</summary>
    public sealed class Building
    {
        internal Building(int id, BuildingType type, Side owner, float2 position, float buildTime, float maxHealth)
        {
            Id = id;
            Type = type;
            Owner = owner;
            Position = position;
            BuildTime = buildTime;
            BuildRemaining = buildTime;
            MaxHealth = maxHealth;
            Health = maxHealth;
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

        private readonly bool[] hiring = new bool[5];

        /// <summary>Включён ли найм этого рода войск.</summary>
        public bool IsHiring(UnitType type)
        {
            return hiring[(int)type];
        }

        /// <summary>Включён ли найм хоть одного рода войск.</summary>
        public bool IsHiringAny => System.Array.IndexOf(hiring, true) >= 0;

        internal void SetHiring(UnitType type, bool value)
        {
            hiring[(int)type] = value;
        }

        /// <summary>С какого рода войск начинать поиск следующего рекрута — так включённые роды нанимаются по очереди.</summary>
        internal int NextHireType { get; set; }

        /// <summary>Сколько осталось до прихода следующего рекрута, s.</summary>
        public float HireRemaining { get; internal set; }

        /// <summary>Сколько домов пристроено к штабу.</summary>
        public int Houses { get; internal set; }

        /// <summary>Сколько осталось строить дом, s; 0 — дом не строится.</summary>
        public float HouseBuildRemaining { get; internal set; }

        public bool IsBuildingHouse => HouseBuildRemaining > 0f;

        public float MaxHealth { get; }

        /// <summary>Здоровье; штурмующие в штыки отряды его снимают, на нуле здание разрушено.</summary>
        public float Health { get; internal set; }

        public bool IsDestroyed => Health <= 0f;

        /// <summary>Чинят ли здание рабочие из штаба в этом тике.</summary>
        public bool IsRepairing { get; internal set; }

        /// <summary>Уровень здания: 1 — обычное, 2 — усиленная казарма.</summary>
        public int Level { get; internal set; } = 1;

        /// <summary>Сколько осталось усиливать казарму, s; 0 — не усиливают.</summary>
        public float UpgradeRemaining { get; internal set; }

        public bool IsUpgrading => UpgradeRemaining > 0f;

        public bool IsUpgraded => Level > 1;

        /// <summary>Сколько осталось готовить заказанную в артдепо пушку, s; 0 — пушку не готовят.</summary>
        public float GunOrderRemaining { get; internal set; }

        public bool IsOrderingGun => GunOrderRemaining > 0f;

        /// <summary>Сколько пушек заказано в очередь за готовящейся (уже оплачены).</summary>
        public int GunOrdersQueued { get; internal set; }

        /// <summary>У здания работают рабочие из штаба: чинят его или строят (видны на поле).</summary>
        public bool HasWorkers { get; internal set; }

        /// <summary>Сколько секунд здание не получало урона.</summary>
        internal float SinceDamage { get; set; }

        /// <summary>Сколько осталось до следующего залпа часовых, s.</summary>
        internal float SentryTimer { get; set; }

        /// <summary>По кому целятся часовые (0 — не целятся); залп — через <see cref="SentryAimRemaining"/> секунд.</summary>
        public int SentryAimTargetId { get; internal set; }

        public float SentryAimRemaining { get; internal set; }

        public bool IsSentryAiming => SentryAimTargetId != 0;
    }
}
