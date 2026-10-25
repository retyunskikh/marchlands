using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    /// <summary>Типовые расстановки для тестов стрельбы.</summary>
    internal static class CombatTestScenarios
    {
        // Половина глубины полной линии (2 шеренги × 0.8 u / 2) — расстояние от центра до фронта.
        private const float LineHalfDepth = 0.8f;

        /// <summary>
        /// Французская линия в начале координат фронтом вверх и австрийская линия фронтом вниз,
        /// фронты разделяет <paramref name="frontDistance"/>.
        /// </summary>
        public static Match FacingLines(ulong seed, float frontDistance, out Squad shooter, out Squad target)
        {
            var match = new Match(seed, TestSettings.Create());
            shooter = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            target = match.AddSquad(Side.Austria, new float2(0f, frontDistance + 2f * LineHalfDepth), -90f, Formation.Line);
            return match;
        }

        /// <summary>Командует залп и выполняет тики до выстрела; возвращает число убитых.</summary>
        public static int FireOnce(Match match, Squad shooter)
        {
            match.Enqueue(new VolleyCommand(shooter.Id));
            for (int i = 0; i < 100; i++)
            {
                match.Tick();
                foreach (var volley in match.LastTickVolleys)
                {
                    if (volley.ShooterId == shooter.Id)
                    {
                        return volley.Casualties;
                    }
                }
            }

            Assert.Fail("Залп так и не произошёл");
            return 0;
        }
    }
}
