using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Morale;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    /// <summary>Опыт (предел морали растёт с убитыми, знаки ветерана) и давление численного перевеса врага.</summary>
    public class VeteranTests
    {
        private static SimulationSettings Settings()
        {
            SimulationSettings settings = TestSettings.Create();
            MoraleSettings morale = settings.morale;
            morale.veteranMaxMoraleBonus = 100f;
            morale.veteranKillScale = 60f;
            morale.veteranRank1Kills = 15f;
            morale.veteranRank2Kills = 45f;
            morale.veteranRank3Kills = 100f;
            morale.pressureRadius = 40f;
            morale.pressureRatio = 1.5f;
            morale.pressureMoraleLoss = 1.5f;
            morale.pressureMaxLoss = 3f;
            morale.pressureMoraleFloor = 35f;
            morale.moraleRegen = 0f;
            morale.moraleBaseRegen = 0f;
            return settings;
        }

        [Test]
        public void MaxMorale_GrowsWithKills_FastFirst_ThenSlower_UpToPlusHundred()
        {
            MoraleSettings morale = Settings().morale;
            Assert.AreEqual(100f, MoraleMath.GetMaxMorale(0f, morale), 1e-4f);
            float first = MoraleMath.GetMaxMorale(20f, morale) - MoraleMath.GetMaxMorale(0f, morale);
            float later = MoraleMath.GetMaxMorale(120f, morale) - MoraleMath.GetMaxMorale(100f, morale);
            Assert.Greater(first, 20f, "первые 20 убитых — заметная прибавка");
            Assert.Less(later, first * 0.3f, "дальше всё медленнее");
            Assert.LessOrEqual(MoraleMath.GetMaxMorale(10000f, morale), 200f + 1e-3f, "не больше +100");
            Assert.Greater(MoraleMath.GetMaxMorale(10000f, morale), 199f);
        }

        [Test]
        public void VeteranRank_ByKills()
        {
            MoraleSettings morale = Settings().morale;
            Assert.AreEqual(0, MoraleMath.GetVeteranRank(14f, morale));
            Assert.AreEqual(1, MoraleMath.GetVeteranRank(15f, morale));
            Assert.AreEqual(2, MoraleMath.GetVeteranRank(45f, morale));
            Assert.AreEqual(3, MoraleMath.GetVeteranRank(100f, morale));
        }

        [Test]
        public void Kills_RaiseMaxMorale_AndCalmRegenFillsAboveHundred()
        {
            SimulationSettings settings = Settings();
            settings.morale.moraleRegen = 5f;
            settings.morale.moraleCalmTime = 0f;
            var match = new Match(1, settings);
            Squad squad = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            MoraleMath.ApplyKills(squad, 50f, settings);

            Assert.AreEqual(50f, squad.Kills, 1e-4f);
            Assert.AreEqual(2, squad.VeteranRank);
            Assert.Greater(squad.MaxMorale, 150f);
            for (int i = 0; i < 300; i++)
            {
                match.Tick();
            }

            Assert.AreEqual(squad.MaxMorale, squad.Morale, 0.01f, "в спокойствии дух дорастает до нового предела");
        }

        [Test]
        public void LoneSquad_AgainstMany_LosesMorale_ButNotBelowFloor()
        {
            var match = new Match(1, Settings());
            Squad lone = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            for (int i = 0; i < 3; i++)
            {
                match.AddSquad(Side.Austria, new float2(-20f + i * 20f, 30f), 270f, Formation.Line);
            }

            for (int i = 0; i < 20; i++)
            {
                match.Tick();
            }

            Assert.Less(lone.Morale, 99f, "один против троих — дух падает");
            for (int i = 0; i < 1200; i++)
            {
                match.Tick();
            }

            Assert.AreEqual(35f, lone.Morale, 0.01f, "одно давление ниже порога не опускает");
            Assert.IsFalse(lone.IsRouting);
        }

        [Test]
        public void WithAlliesNearby_NoPressure()
        {
            var match = new Match(1, Settings());
            Squad squad = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            match.AddSquad(Side.France, new float2(-15f, 0f), 90f, Formation.Line);
            match.AddSquad(Side.France, new float2(15f, 0f), 90f, Formation.Line);
            for (int i = 0; i < 3; i++)
            {
                match.AddSquad(Side.Austria, new float2(-20f + i * 20f, 30f), 270f, Formation.Line);
            }

            for (int i = 0; i < 100; i++)
            {
                match.Tick();
            }

            Assert.AreEqual(100f, squad.Morale, 1e-3f, "силы равны — давления нет");
        }

        [Test]
        public void LeavingEnemies_DoNotPress()
        {
            var match = new Match(1, Settings());
            Squad lone = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            for (int i = 0; i < 3; i++)
            {
                Squad enemy = match.AddSquad(Side.Austria, new float2(-20f + i * 20f, 20f), 90f, Formation.Column);
                match.Enqueue(new MoveCommand(enemy.Id, enemy.Position + new float2(0f, 60f), 90f));
            }

            match.Tick();
            match.Tick();
            float before = lone.Morale;
            for (int i = 0; i < 50; i++)
            {
                match.Tick();
            }

            Assert.AreEqual(before, lone.Morale, 1e-3f, "уходящие прочь не давят");
        }
    }
}
