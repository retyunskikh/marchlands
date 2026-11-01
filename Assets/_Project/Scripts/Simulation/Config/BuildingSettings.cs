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

        /// <summary>Цена усиления казармы (казарма у стороны одна — её усиливают, а не строят вторую).</summary>
        public int barracksUpgradeCost;

        /// <summary>Сколько коней даёт конюшня на каждом уровне (1 — построенная, дальше — расширенная).</summary>
        public int stableHorsesPerLevel;

        /// <summary>До какого уровня расширяется конюшня (3 — до 60 коней при 20 на уровень).</summary>
        public int stableMaxLevel;

        /// <summary>Расширить конюшню: цена до второго уровня и до третьего, сколько строят, s.</summary>
        public int stableExpandCost;

        public int stableSecondExpandCost;

        public float stableExpandTime;

        /// <summary>Сколько секунд усиливают казарму; в это время она не нанимает, s.</summary>
        public float barracksUpgradeTime;

        /// <summary>Во сколько раз быстрее нанимает усиленная казарма.</summary>
        public float barracksUpgradeHireSpeed;

        /// <summary>Укрепить штаб (деревянный — в каменный): цена, сколько строят, s.</summary>
        public int headquartersUpgradeCost;

        public float headquartersUpgradeTime;

        /// <summary>Укрепить блокгауз (деревянный — в каменный): цена, сколько строят, s.</summary>
        public int blockhouseUpgradeCost;

        public float blockhouseUpgradeTime;

        /// <summary>Укреплённый блокгауз: ружей в залпе и дальность огня, u.</summary>
        public int upgradedBlockhouseShots;

        public float upgradedBlockhouseRange;

        /// <summary>Во сколько раз крепче (больше здоровья) укреплённые штаб и блокгауз.</summary>
        public float fortifiedHealthMultiplier;

        /// <summary>Сколько ружей в залпе часовых усиленной казармы.</summary>
        public int upgradedSentryShots;

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

        public int depotCost;

        /// <summary>Время постройки артдепо, s.</summary>
        public float depotBuildTime;

        /// <summary>Здоровье артдепо.</summary>
        public float depotHealth;

        /// <summary>Цена первой пушки (сразу со всем расчётом).</summary>
        public int gunCrewHireCost;

        /// <summary>Сколько секунд готовят заказанную пушку, s.</summary>
        public float gunCrewHireInterval;

        /// <summary>Во сколько раз дороже каждая следующая пушка, пока предыдущие живы.</summary>
        public float gunCostGrowth;

        /// <summary>Сколько пушек можно заказать в депо сверх готовящейся (очередь).</summary>
        public int gunQueueSize;

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

        /// <summary>
        /// Сколько секунд рабочие из штаба заново ставят разрушенный блокгауз. Стройка начинается, когда у руин
        /// repairDelay секунд нет врагов (базу отбили), и стоит, пока враг рядом.
        /// </summary>
        public float blockhouseRebuildTime;

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
