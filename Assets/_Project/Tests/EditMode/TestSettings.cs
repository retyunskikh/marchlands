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
                    jaegerSquads = 2,
                    cavalrySquads = 1,
                    cavalryChargeDistance = 12f
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
                jaegers = new UnitTypeSettings
                {
                    squadSize = 16,
                    minSquadSize = 6,
                    speedMultiplier = 1.5f,
                    accuracyMultiplier = 1.4f,
                    rangeMultiplier = 1.25f,
                    moraleLossMultiplier = 1f,
                    meleeMultiplier = 0.7f,
                    canFire = true,
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
                    maxWaitingSquads = 2,
                    stableCost = 200,
                    stableBuildTime = 40f,
                    cavalryHireCost = 6,
                    cavalryHireInterval = 3f,
                    spawnOffset = 4f
                },
                victory = new VictorySettings
                {
                    hqCaptureRadius = 5f,
                    hqCaptureTime = 30f,
                    dominationShare = 2f / 3f,
                    dominationTime = 180f
                },
                terrain = new TerrainSettings
                {
                    roadSpeedMultiplier = 1.25f,
                    roadHalfWidth = 1.5f,
                    forestSpeedMultiplier = 0.6f,
                    forestCoverMultiplier = 0.7f,
                    pathCellSize = 2f
                }
            };
        }
    }
}
