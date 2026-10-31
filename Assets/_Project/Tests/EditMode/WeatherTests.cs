using System.Collections.Generic;
using System.Linq;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class WeatherTests
    {
        private static int TicksFor(float seconds)
        {
            return (int)math.round(seconds * SimulationTime.TicksPerSecond);
        }

        private static List<bool> RainTimeline(ulong seed)
        {
            var settings = TestSettings.Create();
            settings.weather.dryTimeMin = 20f;
            settings.weather.dryTimeMax = 40f;
            settings.weather.rainTimeMin = 10f;
            settings.weather.rainTimeMax = 20f;
            settings.weather.rainFireChance = 0.67f;
            var match = new Match(seed, settings);
            var timeline = new List<bool>();
            for (int i = 0; i < TicksFor(300f); i++)
            {
                match.Tick();
                timeline.Add(match.IsRaining);
            }

            return timeline;
        }

        [Test]
        public void Rain_ComesAndGoes_Deterministically()
        {
            List<bool> first = RainTimeline(5);
            List<bool> second = RainTimeline(5);

            CollectionAssert.AreEqual(first, second, "Одинаковый seed — одинаковая погода");
            Assert.IsFalse(first.Take(TicksFor(20f) - 1).Any(r => r), "Первые dryTimeMin секунд сухо");
            int changes = first.Zip(first.Skip(1), (a, b) => a != b).Count(c => c);
            Assert.GreaterOrEqual(changes, 4, "За 5 минут дождь начинался и кончался не раз");
        }

        [Test]
        public void Rain_Disabled_WhenNoDryTime()
        {
            var match = new Match(1, TestSettings.Create());
            for (int i = 0; i < TicksFor(600f); i++)
            {
                match.Tick();
                Assert.IsFalse(match.IsRaining);
            }
        }

        [Test]
        public void Rain_AboutAThirdOfMusketsMisfire()
        {
            int ready = 0;
            int fired = 0;
            for (ulong seed = 1; seed <= 60; seed++)
            {
                Match match = CombatTestScenarios.FacingLines(seed, 7f, out Squad shooter, out _);
                match.Settings.weather.rainFireChance = 0.67f;
                match.IsRaining = true;
                match.Enqueue(new VolleyCommand(shooter.Id));
                for (int i = 0; i < 100; i++)
                {
                    match.Tick();
                    VolleyEvent[] volleys = match.LastTickVolleys.Where(v => v.ShooterId == shooter.Id).ToArray();
                    if (volleys.Length > 0)
                    {
                        Assert.AreEqual(VolleyMath.Shooters(Formation.Line, shooter.Alive, match.Settings.squads),
                            volleys[0].Shooters + volleys[0].Misfires, "Выстрелы и осечки — все стрелки");
                        Assert.LessOrEqual(volleys[0].Casualties, volleys[0].Shooters);
                        ready += volleys[0].Shooters + volleys[0].Misfires;
                        fired += volleys[0].Shooters;
                        break;
                    }
                }
            }

            Assert.That((float)fired / ready, Is.EqualTo(0.67f).Within(0.04f));
        }

        [Test]
        public void Dry_AllMusketsFire()
        {
            Match match = CombatTestScenarios.FacingLines(1, 7f, out Squad shooter, out _);
            match.Settings.weather.rainFireChance = 0.67f;
            match.Enqueue(new VolleyCommand(shooter.Id));
            for (int i = 0; i < 100; i++)
            {
                match.Tick();
                if (match.LastTickVolleys.Any(v => v.ShooterId == shooter.Id))
                {
                    Assert.AreEqual(0, match.LastTickVolleys.First(v => v.ShooterId == shooter.Id).Misfires);
                    return;
                }
            }

            Assert.Fail("Залпа не было");
        }
    }
}
