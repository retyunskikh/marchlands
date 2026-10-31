using System;

namespace Marchlands.Simulation.Config
{
    /// <summary>Параметры зданий и найма (раздел 3.6 и 6 docs/design.md).</summary>
    [Serializable]
    public sealed class BuildingSettings
    {
        public int barracksCost;

        /// <summary>Время постройки казармы, s.</summary>
        public float barracksBuildTime;

        /// <summary>Казарму можно ставить не дальше этого от своего штаба, u.</summary>
        public float buildRadiusFromHQ;

        /// <summary>…и не ближе этого к другим зданиям и деревням, u.</summary>
        public float buildingSpacing;

        /// <summary>Цена одного фузилёра.</summary>
        public int hireCost;

        /// <summary>Раз в столько секунд казарма получает фузилёра, s.</summary>
        public float hireInterval;

        /// <summary>Цена одного гренадёра.</summary>
        public int grenadierHireCost;

        /// <summary>Раз в столько секунд казарма получает гренадёра, s.</summary>
        public float grenadierHireInterval;

        /// <summary>Найм рода войск приостанавливается, когда в его резерве набралось людей на столько отрядов.</summary>
        public int maxWaitingSquads;

        public int stableCost;

        /// <summary>Время постройки конюшни, s.</summary>
        public float stableBuildTime;

        /// <summary>Цена одного всадника.</summary>
        public int cavalryHireCost;

        /// <summary>Раз в столько секунд конюшня с включённым наймом получает всадника, s.</summary>
        public float cavalryHireInterval;

        /// <summary>Сколько мест (солдат на поле и рекрутов в резерве) даёт штаб.</summary>
        public int headquartersPopulation;

        /// <summary>Сколько мест добавляет каждый дом-пристройка к штабу.</summary>
        public int housePopulation;

        /// <summary>Цена первого дома; каждый следующий вдвое дороже.</summary>
        public int houseCost;

        /// <summary>Время постройки дома, s.</summary>
        public float houseBuildTime;

        /// <summary>Сколько домов можно пристроить к штабу.</summary>
        public int maxHouses;

        /// <summary>Насколько дальше обычного (buildingSpacing) от штаба держатся новые здания — там пристройки, u.</summary>
        public float annexSpacing;

        /// <summary>На каком расстоянии от казармы появляется нанятый отряд, u.</summary>
        public float spawnOffset;

        /// <summary>На каком расстоянии перед зданием стоит толпа ждущих людей, u.</summary>
        public float campOffset;

        /// <summary>Здоровье штаба.</summary>
        public float headquartersHealth;

        /// <summary>Здоровье казармы.</summary>
        public float barracksHealth;

        /// <summary>Здоровье конюшни.</summary>
        public float stableHealth;

        /// <summary>Сколько часовых стреляет одним залпом из каждого построенного здания.</summary>
        public int sentryShots;

        /// <summary>Раз в столько секунд часовые здания дают залп, s.</summary>
        public float sentryInterval;

        /// <summary>Вероятность попадания одного часового (до множителей колонны, укрытия и рода войск цели).</summary>
        public float sentryHitChance;

        /// <summary>Часовые стреляют по врагу, чей центр ближе этого к центру здания, u.</summary>
        public float sentryRange;

        /// <summary>Здоровье блокгауза.</summary>
        public float blockhouseHealth;

        /// <summary>Сколько ружей стреляет одним залпом из блокгауза (из бойниц; стрелков не видно).</summary>
        public int blockhouseShots;

        /// <summary>Раз в столько секунд блокгауз даёт залп, s.</summary>
        public float blockhouseInterval;

        /// <summary>Вероятность попадания одного стрелка блокгауза (до множителей колонны, укрытия и рода войск цели).</summary>
        public float blockhouseHitChance;

        /// <summary>Блокгауз стреляет по врагу, чей центр ближе этого к центру блокгауза, u.</summary>
        public float blockhouseRange;

        /// <summary>Отряд бьёт здание в штыки, когда его центр ближе этого к центру здания, u.</summary>
        public float assaultRange;

        /// <summary>Сколько здоровья здания снимает в секунду один солдат штурмующего отряда (× meleeMultiplier рода войск).</summary>
        public float assaultDamage;

        /// <summary>Через столько секунд без урона рабочие из штаба начинают чинить здание, s.</summary>
        public float repairDelay;

        /// <summary>Сколько здоровья в секунду восстанавливают рабочие.</summary>
        public float repairRate;

        /// <summary>Пока не бегущий враг ближе этого к центру здания, его не чинят, u.</summary>
        public float repairEnemyRadius;
    }
}
