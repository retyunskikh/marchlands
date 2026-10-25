using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Morale;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class MoraleTests
    {
        private const int SeedCount = 500;

        private static int TicksFor(float seconds)
        {
            return (int)math.round(seconds * SimulationTime.TicksPerSecond);
        }

        private static void RunTicks(Match match, int count)
        {
            for (int i = 0; i < count; i++)
            {
                match.Tick();
            }
        }

        [Test]
        public void Loss_FollowsFormulaWithMultipliers()
        {
            MoraleSettings settings = TestSettings.Create().morale;
            // 10 из 36: 10/36 × 100 × 1.5 = 41.67.
            float baseLoss = 10f / 36f * 100f * 1.5f;

            Assert.AreEqual(baseLoss, MoraleMath.Loss(10, 36, false, false, settings), 1e-3f);
            Assert.AreEqual(baseLoss * 1.5f, MoraleMath.Loss(10, 36, true, false, settings), 1e-3f);
            Assert.AreEqual(baseLoss * 1.5f, MoraleMath.Loss(10, 36, false, true, settings), 1e-3f);
            Assert.AreEqual(baseLoss * 2.25f, MoraleMath.Loss(10, 36, true, true, settings), 1e-3f);
            Assert.AreEqual(0f, MoraleMath.Loss(0, 36, true, true, settings));
        }

        [TestCase(0f, 10f, false, TestName = "Flank_StraightAhead_IsFront")]
        [TestCase(5f, 10f, false, TestName = "Flank_InsideFrontalArc_IsFront")]
        [TestCase(10f, 3f, true, TestName = "Flank_Side")]
        [TestCase(0f, -10f, true, TestName = "Flank_Rear")]
        public void IsFlank_UsesFrontalArc(float x, float y, bool expected)
        {
            var match = new Match(1, TestSettings.Create());
            Squad target = match.AddSquad(Side.Austria, float2.zero, 90f, Formation.Line);

            Assert.AreEqual(expected, MoraleMath.IsFlank(target, new float2(x, y), match.Settings.morale));
        }

        [Test]
        public void FirstVolleyAtPointBlank_RoutsFullSquad_InAtLeast90PercentOfSeeds()
        {
            int routed = 0;
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                Match match = CombatTestScenarios.FacingLines(seed, 3f, out Squad shooter, out Squad target);
                CombatTestScenarios.FireOnce(match, shooter);
                if (target.IsRouting || match.FindSquad(target.Id) == null)
                {
                    routed++;
                }
            }

            Assert.GreaterOrEqual(routed, SeedCount * 0.9, $"Обращено в бегство {routed} из {SeedCount}");
        }

        [Test]
        public void FirstVolleyAtEffectiveRange_HurtsMoraleButRarelyRouts()
        {
            int routed = 0;
            double totalLoss = 0;
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                Match match = CombatTestScenarios.FacingLines(seed, 7f, out Squad shooter, out Squad target);
                CombatTestScenarios.FireOnce(match, shooter);
                totalLoss += MoraleMath.MaxMorale - target.Morale;
                if (target.IsRouting)
                {
                    routed++;
                }
            }

            Assert.LessOrEqual(routed, SeedCount * 0.1, $"Обращено в бегство {routed} из {SeedCount}");
            Assert.Greater(totalLoss / SeedCount, 30.0, "Первый залп на эффективной дистанции должен заметно бить по морали");
        }

        [Test]
        public void RoutThreshold_SeparatesSteadyFromRouting()
        {
            var match = new Match(1, TestSettings.Create());
            Squad atThreshold = match.AddSquad(Side.France, new float2(-20f, 0f), 90f, Formation.Line);
            Squad below = match.AddSquad(Side.France, new float2(20f, 0f), 90f, Formation.Line);
            atThreshold.Morale = 15f;
            below.Morale = 14.9f;

            match.Tick();

            Assert.AreEqual(MoraleState.Steady, atThreshold.MoraleState);
            Assert.AreEqual(MoraleState.Routing, below.MoraleState);
        }

        [Test]
        public void RoutingSquad_IgnoresOrders_AndRunsToRetreatPointAtRoutSpeed()
        {
            var match = new Match(1, TestSettings.Create());
            match.SetRetreatPoint(Side.France, new float2(0f, -30f));
            Squad squad = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            squad.Morale = 5f;
            match.Tick();
            Assert.IsTrue(squad.IsRouting);

            match.Enqueue(new MoveCommand(squad.Id, new float2(0f, 20f), 90f));
            match.Enqueue(new VolleyCommand(squad.Id));
            float before = squad.Position.y;
            match.Tick();

            Assert.AreEqual(-0.2f, squad.Position.y - before, 1e-4f, "2 u/s × 0.1 s в сторону точки отхода");
            Assert.AreEqual(0f, Angles.Delta(squad.Facing, -90f), 1e-3f, "Бегущий сразу разворачивается к точке отхода");
            Assert.AreEqual(SquadOrderType.None, squad.Order.Type);
        }

        [Test]
        public void RoutingSquad_ReachingRetreatPoint_JoinsReserve()
        {
            var match = new Match(1, TestSettings.Create());
            match.SetRetreatPoint(Side.France, new float2(0f, -3f));
            Squad squad = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            squad.Morale = 5f;

            RunTicks(match, 50);

            Assert.IsNull(match.FindSquad(squad.Id), "Добежавший отряд уходит с поля");
            Assert.AreEqual(36, match.GetPlayer(Side.France).Reserve);
        }

        [Test]
        public void Regen_StartsOnlyAfterCalmTime()
        {
            var match = new Match(1, TestSettings.Create());
            Squad squad = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            squad.Morale = 60f;

            RunTicks(match, TicksFor(10f) - 2);
            Assert.AreEqual(60f, squad.Morale, 1e-3f, "До moraleCalmTime без потерь мораль не растёт");

            RunTicks(match, TicksFor(5f) + 2);
            Assert.AreEqual(70f, squad.Morale, 0.3f, "+2 в секунду");
        }

        [Test]
        public void Regen_IsBlockedByNearbyEnemy()
        {
            var near = new Match(1, TestSettings.Create());
            Squad blocked = near.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            near.AddSquad(Side.Austria, new float2(0f, 9f), -90f, Formation.Line);
            blocked.Morale = 60f;

            var far = new Match(1, TestSettings.Create());
            Squad calm = far.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            far.AddSquad(Side.Austria, new float2(0f, 11f), -90f, Formation.Line);
            calm.Morale = 60f;

            RunTicks(near, TicksFor(20f));
            RunTicks(far, TicksFor(20f));

            Assert.AreEqual(60f, blocked.Morale, 1e-3f, "Враг в 9 u мешает восстановлению");
            Assert.Greater(calm.Morale, 60f, "Враг в 11 u не мешает");
        }

        [Test]
        public void Losses_ResetCalmTimer()
        {
            // Центры в 9 + 1.6 = 10.6 u — дальше calmEnemyRadius, так что стрелок сам восстановлению не мешает.
            Match match = CombatTestScenarios.FacingLines(1, 9f, out Squad shooter, out Squad target);
            RunTicks(match, TicksFor(12f));
            CombatTestScenarios.FireOnce(match, shooter);
            float afterVolley = target.Morale;
            Assert.Less(afterVolley, MoraleMath.MaxMorale);

            RunTicks(match, TicksFor(9f));
            Assert.AreEqual(afterVolley, target.Morale, 1e-3f, "После потерь отсчёт спокойствия начинается заново");
        }

        [Test]
        public void AllyRout_PenalisesAlliesWithinRadiusOnce()
        {
            var match = new Match(1, TestSettings.Create());
            Squad routing = match.AddSquad(Side.France, float2.zero, 90f, Formation.Column);
            Squad close = match.AddSquad(Side.France, new float2(6f, 0f), 90f, Formation.Column);
            Squad far = match.AddSquad(Side.France, new float2(-9f, 0f), 90f, Formation.Column);
            routing.Morale = 5f;

            match.Tick();
            Assert.AreEqual(90f, close.Morale, 1e-3f);
            Assert.AreEqual(100f, far.Morale, 1e-3f);

            RunTicks(match, 20);
            Assert.AreEqual(90f, close.Morale, 1e-3f, "Штраф разовый");
        }

        [Test]
        public void AllyRout_CanCascade()
        {
            var match = new Match(1, TestSettings.Create());
            Squad routing = match.AddSquad(Side.France, float2.zero, 90f, Formation.Column);
            Squad shaken = match.AddSquad(Side.France, new float2(6f, 0f), 90f, Formation.Column);
            routing.Morale = 5f;
            shaken.Morale = 20f;

            match.Tick();

            Assert.IsTrue(shaken.IsRouting);
        }

        [Test]
        public void RoutingSquad_NeverRalliesInField()
        {
            var match = new Match(1, TestSettings.Create());
            match.SetRetreatPoint(Side.France, new float2(0f, -1000f));
            Squad squad = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            squad.Morale = 10f;

            RunTicks(match, TicksFor(60f));

            Assert.IsTrue(squad.IsRouting, "Сбора в поле нет: бегущий бежит до своих");
        }

        [Test]
        public void SquadBelowMinSize_Routs_EvenWithFullMorale()
        {
            var match = new Match(1, TestSettings.Create());
            Squad small = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line, 11);
            Squad enough = match.AddSquad(Side.France, new float2(30f, 0f), 90f, Formation.Line, 12);

            match.Tick();

            Assert.IsTrue(small.IsRouting, "Меньше minSquadSize — уже не отряд");
            Assert.IsFalse(enough.IsRouting);
        }

        [Test]
        public void SquadShotBelowMinSize_Routs()
        {
            var match = new Match(1, TestSettings.Create());
            Squad squad = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line, 14);
            match.Tick();
            Assert.IsFalse(squad.IsRouting);

            squad.Alive = 11;
            match.Tick();

            Assert.IsTrue(squad.IsRouting);
        }

        [Test]
        public void RoutingSquad_CannotFire()
        {
            Match match = CombatTestScenarios.FacingLines(1, 5f, out Squad shooter, out _);
            shooter.Morale = 5f;
            match.Tick();

            Assert.IsFalse(Marchlands.Simulation.Combat.VolleyRules.CanVolley(match, shooter));
        }
    }
}
