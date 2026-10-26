using System.Linq;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class FireSystemTests
    {
        private const int SeedCount = 1000;

        private static int TicksFor(float seconds)
        {
            return (int)math.round(seconds * SimulationTime.TicksPerSecond);
        }

        private static double AverageFirstVolley(float frontDistance)
        {
            long total = 0;
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                Match match = CombatTestScenarios.FacingLines(seed, frontDistance, out Squad shooter, out _);
                total += CombatTestScenarios.FireOnce(match, shooter);
            }

            return (double)total / SeedCount;
        }

        [Test]
        public void FirstVolley_AtEffectiveRange_KillsAboutTenPointEight()
        {
            Assert.That(AverageFirstVolley(7f), Is.InRange(9.5, 12.0));
        }

        [Test]
        public void FirstVolley_AtPointBlank_KillsAboutTwentyOnePointSix()
        {
            Assert.That(AverageFirstVolley(3f), Is.InRange(19.5, 23.5));
        }

        [Test]
        public void SecondVolley_HasNoFirstVolleyBonus()
        {
            long total = 0;
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                Match match = CombatTestScenarios.FacingLines(seed, 7f, out Squad shooter, out _);
                CombatTestScenarios.FireOnce(match, shooter);
                Assert.IsFalse(shooter.IsFirstVolley);

                while (shooter.FireState != FireState.Loaded)
                {
                    match.Tick();
                }

                total += CombatTestScenarios.FireOnce(match, shooter);
            }

            // 36 × 0.20 = 7.2 без бонуса; с бонусом было бы 10.8.
            Assert.That((double)total / SeedCount, Is.InRange(6.2, 8.2));
        }

        [Test]
        public void Volley_AimsForAimTime_ThenReloadsForReloadTime()
        {
            Match match = CombatTestScenarios.FacingLines(1, 7f, out Squad shooter, out _);
            match.Enqueue(new VolleyCommand(shooter.Id));

            int ticks = 0;
            while (!match.LastTickVolleys.Any())
            {
                match.Tick();
                ticks++;
                Assert.Less(ticks, 100);
                if (!match.LastTickVolleys.Any())
                {
                    Assert.AreEqual(FireState.Aiming, shooter.FireState, $"Тик {ticks}");
                }
            }

            Assert.That(ticks, Is.InRange(TicksFor(0.8f) - 1, TicksFor(0.8f) + 1), "Прицеливание");
            Assert.AreEqual(FireState.Reloading, shooter.FireState);

            int reloadTicks = 0;
            while (shooter.FireState == FireState.Reloading)
            {
                match.Tick();
                reloadTicks++;
                Assert.Less(reloadTicks, 1000);
            }

            Assert.That(reloadTicks, Is.InRange(TicksFor(14f) - 1, TicksFor(14f) + 1), "Перезарядка");
            Assert.AreEqual(FireState.Loaded, shooter.FireState);
        }

        [Test]
        public void CanVolley_IsFalseWhileAimingAndReloading_TrueWhenLoaded()
        {
            Match match = CombatTestScenarios.FacingLines(1, 7f, out Squad shooter, out _);
            Assert.IsTrue(VolleyRules.CanVolley(match, shooter));

            match.Enqueue(new VolleyCommand(shooter.Id));
            match.Tick();
            Assert.AreEqual(FireState.Aiming, shooter.FireState);
            Assert.IsFalse(VolleyRules.CanVolley(match, shooter));

            CombatTestScenarios.FireOnce(match, shooter);
            Assert.AreEqual(FireState.Reloading, shooter.FireState);
            Assert.IsFalse(VolleyRules.CanVolley(match, shooter));

            while (shooter.FireState != FireState.Loaded)
            {
                match.Tick();
            }

            Assert.IsTrue(VolleyRules.CanVolley(match, shooter));
        }

        [Test]
        public void NoVolley_BeyondMaxRange()
        {
            Match match = CombatTestScenarios.FacingLines(1, 12.5f, out Squad shooter, out _);

            Assert.IsFalse(VolleyRules.CanVolley(match, shooter));
            match.Enqueue(new VolleyCommand(shooter.Id));
            RunTicks(match, 30);

            Assert.AreEqual(FireState.Loaded, shooter.FireState);
            Assert.IsTrue(shooter.IsFirstVolley);
        }

        [Test]
        public void NoVolley_OutsideSector()
        {
            var match = new Match(1, TestSettings.Create());
            Squad shooter = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            // Враг сбоку-сзади: близко, но вне сектора огня.
            match.AddSquad(Side.Austria, new float2(10f, -2f), 180f, Formation.Line);

            Assert.IsFalse(VolleyRules.CanVolley(match, shooter));
            match.Enqueue(new VolleyCommand(shooter.Id));
            RunTicks(match, 30);

            Assert.AreEqual(FireState.Loaded, shooter.FireState);
        }

        [Test]
        public void NoVolley_AtAllies()
        {
            var match = new Match(1, TestSettings.Create());
            Squad shooter = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            match.AddSquad(Side.France, new float2(0f, 6f), -90f, Formation.Line);

            Assert.IsFalse(VolleyRules.CanVolley(match, shooter));
        }

        [Test]
        public void Squad_NeverFiresWithoutOrder()
        {
            Match match = CombatTestScenarios.FacingLines(1, 5f, out Squad shooter, out _);

            RunTicks(match, 100);

            Assert.AreEqual(FireState.Loaded, shooter.FireState);
            Assert.IsTrue(shooter.IsFirstVolley);
        }

        [Test]
        public void FormationChange_CancelsAiming_SquadStaysLoaded()
        {
            Match match = CombatTestScenarios.FacingLines(1, 7f, out Squad shooter, out Squad target);
            match.Enqueue(new VolleyCommand(shooter.Id));
            RunTicks(match, 3);
            match.Enqueue(new ChangeFormationCommand(shooter.Id, Formation.Column));

            RunTicks(match, TicksFor(4f) + 5);

            Assert.AreEqual(FireState.Loaded, shooter.FireState);
            Assert.IsTrue(shooter.IsFirstVolley);
            Assert.AreEqual(36, target.Alive);
        }

        [Test]
        public void VolleyCommand_IsIgnoredDuringFormationChange()
        {
            Match match = CombatTestScenarios.FacingLines(1, 7f, out Squad shooter, out _);
            match.Enqueue(new ChangeFormationCommand(shooter.Id, Formation.Column));
            match.Tick();

            Assert.IsFalse(VolleyRules.CanVolley(match, shooter));
            match.Enqueue(new VolleyCommand(shooter.Id));
            match.Tick();

            Assert.AreEqual(FireState.Loaded, shooter.FireState);
        }

        [Test]
        public void Aiming_HaltsMovement_ThenReloadSlowsIt()
        {
            Match match = CombatTestScenarios.FacingLines(1, 10f, out Squad shooter, out _);
            match.Enqueue(new MoveCommand(shooter.Id, new float2(0f, 3f), 90f));
            match.Enqueue(new VolleyCommand(shooter.Id));

            for (int i = 0; i < TicksFor(0.8f) - 1; i++)
            {
                match.Tick();
                Assert.AreEqual(0f, shooter.Position.y, 1e-5f, $"Отряд двигался во время прицеливания, тик {i}");
            }

            while (shooter.FireState != FireState.Reloading)
            {
                match.Tick();
            }

            float before = shooter.Position.y;
            match.Tick();
            float step = shooter.Position.y - before;

            // Линия: 1 u/s × 0.6 × 0.1 s.
            Assert.AreEqual(0.06f, step, 1e-4f);
        }

        [Test]
        public void MutualVolley_UsesStrengthAtTickStart()
        {
            for (ulong seed = 1; seed <= 50; seed++)
            {
                Match match = CombatTestScenarios.FacingLines(seed, 3f, out Squad french, out Squad austrian);
                match.Enqueue(new VolleyCommand(french.Id));
                match.Enqueue(new VolleyCommand(austrian.Id));

                while (match.LastTickVolleys.Count == 0)
                {
                    match.Tick();
                }

                Assert.AreEqual(2, match.LastTickVolleys.Count, "Оба залпа в одном тике");
                Assert.IsTrue(match.LastTickVolleys.All(v => v.Shooters == 36), "Каждая сторона стреляет полным составом");
            }
        }

        [Test]
        public void DestroyedSquad_IsRemovedFromMatch()
        {
            for (ulong seed = 1; seed <= 50; seed++)
            {
                var match = new Match(seed, TestSettings.Create());
                Squad shooter = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
                Squad target = match.AddSquad(Side.Austria, new float2(0f, 3f), -90f, Formation.Line, 1);

                int casualties = CombatTestScenarios.FireOnce(match, shooter);
                if (casualties == 1)
                {
                    Assert.IsNull(match.FindSquad(target.Id));
                    Assert.IsFalse(match.Squads.Contains(target));
                    return;
                }
            }

            Assert.Fail("Ни в одном из 50 seed цель из одного солдата не погибла в упор");
        }

        [Test]
        public void SameSeed_GivesSameCasualties()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                Match first = CombatTestScenarios.FacingLines(seed, 5f, out Squad a, out _);
                Match second = CombatTestScenarios.FacingLines(seed, 5f, out Squad b, out _);

                Assert.AreEqual(CombatTestScenarios.FireOnce(first, a), CombatTestScenarios.FireOnce(second, b));
            }
        }

        private static void RunTicks(Match match, int count)
        {
            for (int i = 0; i < count; i++)
            {
                match.Tick();
            }
        }
    }
}
