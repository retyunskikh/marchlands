using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Morale;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class UnitTypeTests
    {
        private const float LineHalfDepth = 0.8f;

        private static void RunTicks(Match match, int count)
        {
            for (int i = 0; i < count; i++)
            {
                match.Tick();
            }
        }

        [Test]
        public void AddSquad_UsesSizeOfUnitType()
        {
            var match = new Match(1, TestSettings.Create());

            Assert.AreEqual(36, match.AddSquad(Side.France, float2.zero, 90f, Formation.Line).Alive);
            Assert.AreEqual(UnitType.Grenadiers, match.Squads[0].Type, "По умолчанию — гренадёры");
            Squad jaegers = match.AddSquad(Side.France, new float2(20f, 0f), 90f, Formation.Line, UnitType.Jaegers);
            Assert.AreEqual(16, jaegers.Alive);
            Assert.AreEqual(UnitType.Jaegers, jaegers.Type);
        }

        [Test]
        public void Jaegers_MarchFaster()
        {
            var match = new Match(1, TestSettings.Create());
            Squad grenadiers = match.AddSquad(Side.France, new float2(-10f, 0f), 90f, Formation.Column);
            Squad jaegers = match.AddSquad(Side.France, new float2(10f, 0f), 90f, Formation.Column, UnitType.Jaegers);
            match.Enqueue(new MoveCommand(grenadiers.Id, new float2(-10f, 50f), 90f));
            match.Enqueue(new MoveCommand(jaegers.Id, new float2(10f, 50f), 90f));

            RunTicks(match, 50);

            Assert.AreEqual(1.5f, jaegers.Position.y / grenadiers.Position.y, 0.02f);
        }

        [Test]
        public void Jaegers_ReachFartherThanGrenadiers()
        {
            var match = new Match(1, TestSettings.Create());
            // Фронты в 13 u: дальше максимальной дальности гренадёров (12), ближе — егерей (15).
            Squad grenadiers = match.AddSquad(Side.France, new float2(-30f, 0f), 90f, Formation.Line);
            Squad jaegers = match.AddSquad(Side.France, new float2(30f, 0f), 90f, Formation.Line, UnitType.Jaegers);
            match.AddSquad(Side.Austria, new float2(-30f, 13f + 2f * LineHalfDepth), -90f, Formation.Line);
            match.AddSquad(Side.Austria, new float2(30f, 13f + 2f * LineHalfDepth), -90f, Formation.Line);

            Assert.IsFalse(VolleyRules.CanVolley(match, grenadiers));
            Assert.IsTrue(VolleyRules.CanVolley(match, jaegers));
        }

        [Test]
        public void Jaegers_HitMoreOftenPerShooter()
        {
            float grenadierHits = 0f;
            float jaegerHits = 0f;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                grenadierHits += HitsPerShooter(seed, UnitType.Grenadiers);
                jaegerHits += HitsPerShooter(seed, UnitType.Jaegers);
            }

            // На 5 u егерь бьёт, как гренадёр на 4 u, и в 1.4 раза метче: в сумме заметно больше 1.4.
            Assert.Greater(jaegerHits / grenadierHits, 1.4f);
        }

        private static float HitsPerShooter(ulong seed, UnitType type)
        {
            var match = new Match(seed, TestSettings.Create());
            Squad shooter = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line, type);
            match.AddSquad(Side.Austria, new float2(0f, 5f + 2f * LineHalfDepth), -90f, Formation.Line);
            return (float)CombatTestScenarios.FireOnce(match, shooter) / shooter.Alive;
        }

        [Test]
        public void MoraleLoss_UsesUnitMultiplier()
        {
            SimulationSettings settings = TestSettings.Create();
            settings.grenadiers.moraleLossMultiplier = 0.8f;
            var match = new Match(1, settings);
            Squad squad = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);

            MoraleMath.ApplyLoss(squad, 10, 36, false, false, settings);

            float expected = 100f - MoraleMath.Loss(10, 36, false, false, settings.morale) * 0.8f;
            Assert.AreEqual(expected, squad.Morale, 1e-3f);
        }

        [Test]
        public void Jaegers_StaySquadWithFewerMen()
        {
            var match = new Match(1, TestSettings.Create());
            Squad enough = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line, UnitType.Jaegers, 6);
            Squad tooFew = match.AddSquad(Side.France, new float2(30f, 0f), 90f, Formation.Line, UnitType.Jaegers, 5);

            match.Tick();

            Assert.IsFalse(enough.IsRouting, "У егерей отряд — от 6 человек");
            Assert.IsTrue(tooFew.IsRouting);
        }
    }
}
