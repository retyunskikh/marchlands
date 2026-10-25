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
                }
            };
        }
    }
}
