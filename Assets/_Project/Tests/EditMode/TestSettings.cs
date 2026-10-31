using Marchlands.Simulation.Config;

namespace Marchlands.Tests.EditMode
{
    /// <summary>
    /// Стартовые значения из раздела 6 docs/design.md. Тесты проверяют механику на них и не зависят
    /// от того, как баланс настроен в ассете.
    /// </summary>
    internal static class TestSettings
    {
        public static SimulationSettings Create()
        {
            return new SimulationSettings
            {
                squads = new SquadSettings
                {
                    lineRanks = 2,
                    columnFrontWidth = 6,
                    fileSpacing = 0.5f,
                    rankSpacing = 0.8f,
                    lineSpeed = 1.0f,
                    columnSpeed = 1.6f,
                    rotationSpeed = 45f,
                    formationChangeTime = 4f,
                    reformMoveSpeedMultiplier = 0.4f,
                    arrivalTolerance = 0.05f
                },
                combat = new CombatSettings
                {
                    maxRange = 12f,
                    effectiveRange = 7f,
                    pointBlankRange = 3f,
                    hitMax = 0.05f,
                    hitEffective = 0.20f,
                    hitPointBlank = 0.40f,
                    firstVolleyMultiplier = 1.5f,
                    columnTargetMultiplier = 1.3f,
                    fireArcHalfAngle = 45f,
                    aimTime = 0.8f,
                    reloadTime = 14f,
                    reloadMoveSpeedMultiplier = 0.6f
                },
                morale = new MoraleSettings
                {
                    moraleShockFactor = 1.5f,
                    pointBlankMoraleMultiplier = 1.5f,
                    flankMoraleMultiplier = 1.5f,
                    frontalArcHalfAngle = 60f,
                    allyRoutRadius = 8f,
                    allyRoutPenalty = 10f,
                    enemyRoutRadius = 12f,
                    enemyRoutBonus = 15f,
                    moraleRegen = 2f,
                    moraleBaseRegen = 0f,
                    killMoraleGain = 0f,
                    reinforceMoraleGain = 0f,
                    moraleCalmTime = 10f,
                    calmEnemyRadius = 10f,
                    routThreshold = 15f,
                    routSpeed = 2f,
                    routRotationSpeed = 720f
                },
                melee = new MeleeSettings
                {
                    chargeSpeed = 2.5f,
                    chargeCheckDistance = 4f,
                    chargeMoraleThreshold = 50f,
                    meleeKillRate = 0.04f,
                    chargeBonus = 1.5f,
                    chargeBonusDuration = 3f,
                    flankMeleeMultiplier = 1.5f,
                    contactDistance = 0.5f,
                    chargeMaxDuration = 10f
                },
                ai = new AiSettings
                {
                    reactionDelay = 0.8f,
                    thinkInterval = 0.5f,
                    chargeDistance = 8f,
                    chargeMinMorale = 60f,
                    retreatMorale = 40f,
                    returnMorale = 70f,
                    repathDistance = 1f,
                    engageRadius = 15f,
                    defenseRadius = 20f,
                    directApproachDistance = 12f,
                    cavalrySquads = 1,
                    houseMargin = 10,
                    cavalryChargeDistance = 12f,
                    opportunityVillageDistance = 15f,
                    volleyDelayMin = 0f,
                    volleyDelayMax = 2.5f
                },
                grenadiers = new UnitTypeSettings
                {
                    squadSize = 36,
                    minSquadSize = 12,
                    speedMultiplier = 1f,
                    accuracyMultiplier = 1f,
                    rangeMultiplier = 1f,
                    moraleLossMultiplier = 1f,
                    meleeMultiplier = 1f,
                    canFire = true,
                    chargeBonusMultiplier = 1f,
                    targetHitMultiplier = 1f
                },
                fusiliers = new UnitTypeSettings
                {
                    squadSize = 16,
                    minSquadSize = 6,
                    speedMultiplier = 1f,
                    accuracyMultiplier = 1f,
                    rangeMultiplier = 1f,
                    moraleLossMultiplier = 1f,
                    meleeMultiplier = 1f,
                    canFire = true,
                    chargeBonusMultiplier = 1f,
                    targetHitMultiplier = 1f
                },
                recruits = new UnitTypeSettings
                {
                    squadSize = 0,
                    minSquadSize = 0,
                    speedMultiplier = 0f,
                    accuracyMultiplier = 0.75f,
                    rangeMultiplier = 1f,
                    moraleLossMultiplier = 0f,
                    meleeMultiplier = 0.75f,
                    canFire = false,
                    skirmishFire = true,
                    chargeBonusMultiplier = 1f,
                    targetHitMultiplier = 1f
                },
                cavalry = new UnitTypeSettings
                {
                    squadSize = 24,
                    minSquadSize = 8,
                    speedMultiplier = 2.2f,
                    accuracyMultiplier = 1f,
                    rangeMultiplier = 1f,
                    moraleLossMultiplier = 1f,
                    meleeMultiplier = 2f,
                    canFire = false,
                    chargeBonusMultiplier = 2f,
                    targetHitMultiplier = 1.3f
                },
                economy = new EconomySettings
                {
                    captureRadius = 4f,
                    captureTime = 15f,
                    captureDecayRate = 1f,
                    incomeInterval = 5f,
                    villageIncome = 3,
                    hqIncome = 2,
                    startGold = 150,
                    reinforceInterval = 6f
                },
                buildings = new BuildingSettings
                {
                    barracksCost = 120,
                    barracksBuildTime = 30f,
                    buildRadiusFromHQ = 10f,
                    buildingSpacing = 3f,
                    hireCost = 2,
                    hireInterval = 2f,
                    grenadierHireCost = 2,
                    grenadierHireInterval = 2f,
                    maxWaitingSquads = 2,
                    headquartersPopulation = 1000,
                    housePopulation = 30,
                    houseCost = 80,
                    houseBuildTime = 15f,
                    maxHouses = 4,
                    annexSpacing = 0f,
                    stableCost = 200,
                    stableBuildTime = 40f,
                    cavalryHireCost = 6,
                    cavalryHireInterval = 3f,
                    spawnOffset = 4f,
                    campOffset = 3f,
                    headquartersHealth = 600f,
                    barracksHealth = 300f,
                    stableHealth = 300f,
                    sentryShots = 4,
                    sentryInterval = 6f,
                    sentryHitChance = 0.2f,
                    sentryRange = 10f,
                    blockhouseHealth = 250f,
                    blockhouseShots = 6,
                    blockhouseInterval = 6f,
                    blockhouseHitChance = 0.22f,
                    blockhouseRange = 11f,
                    assaultRange = 3.5f,
                    assaultDamage = 0.5f,
                    repairDelay = 15f,
                    repairRate = 5f,
                    repairEnemyRadius = 12f
                },
                victory = new VictorySettings
                {
                    dominationShare = 2f / 3f,
                    dominationTime = 180f
                },
                terrain = new TerrainSettings
                {
                    roadSpeedMultiplier = 1.25f,
                    roadHalfWidth = 1.5f,
                    forestSpeedMultiplier = 0.6f,
                    forestCoverMultiplier = 0.7f,
                    pathCellSize = 2f,
                    fordSpeedMultiplier = 0.5f,
                    fordTargetMultiplier = 1.3f,
                    hillRangeMultiplier = 1.2f,
                    uphillChargeMultiplier = 0.8f
                }
            };
        }
    }
}
