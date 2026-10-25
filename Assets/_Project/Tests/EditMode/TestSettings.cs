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
                    squadSize = 36,
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
                    moraleRegen = 2f,
                    moraleCalmTime = 10f,
                    calmEnemyRadius = 10f,
                    routThreshold = 15f,
                    rallyThreshold = 50f,
                    routSpeed = 2f,
                    disbandSize = 5
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
                    engageRadius = 15f
                },
                economy = new EconomySettings
                {
                    captureRadius = 4f,
                    captureTime = 15f,
                    captureDecayRate = 1f,
                    incomeInterval = 5f,
                    villageIncome = 3,
                    hqIncome = 2,
                    startGold = 150
                }
            };
        }
    }
}
