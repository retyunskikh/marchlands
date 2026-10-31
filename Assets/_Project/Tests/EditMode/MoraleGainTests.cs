using System.Linq;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Morale;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class MoraleGainTests
    {
        private static int TicksFor(float seconds)
        {
            return (int)math.round(seconds * SimulationTime.TicksPerSecond);
        }

        [Test]
        public void Volley_RaisesShooterMorale_ByKills()
        {
            Match match = CombatTestScenarios.FacingLines(3, 7f, out Squad shooter, out _);
            match.Settings.morale.killMoraleGain = 0.8f;
            shooter.Morale = 50f;
            match.Enqueue(new VolleyCommand(shooter.Id));
            for (int i = 0; i < 100; i++)
            {
                match.Tick();
                VolleyEvent volley = match.LastTickVolleys.FirstOrDefault(v => v.ShooterId == shooter.Id);
                if (volley.ShooterId == shooter.Id)
                {
                    Assert.Greater(volley.Casualties, 0);
                    Assert.AreEqual(50f + volley.Casualties * 0.8f, shooter.Morale, 1e-3f, "Дух растёт на killMoraleGain за убитого");
                    return;
                }
            }

            Assert.Fail("Залпа не было");
        }

        [Test]
        public void Morale_RecoversSlowly_EvenInCombat()
        {
            Match match = CombatTestScenarios.FacingLines(1, 5f, out Squad squad, out _);
            match.Settings.morale.moraleBaseRegen = 0.25f;
            squad.Morale = 50f;

            for (int i = 0; i < TicksFor(10f); i++)
            {
                match.Tick();
            }

            // Враг рядом — не спокойно: только медленное восстановление.
            Assert.AreEqual(50f + 0.25f * 10f, squad.Morale, 0.05f);
        }

        [Test]
        public void Morale_RecoversFast_WhenCalm()
        {
            var match = new Match(1, TestSettings.Create());
            match.Settings.morale.moraleBaseRegen = 0.25f;
            Squad squad = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            squad.Morale = 20f;
            // Потерь давно не было — отряд в спокойствии с первого тика.
            squad.TimeSinceLoss = match.Settings.morale.moraleCalmTime + 1f;

            for (int i = 0; i < TicksFor(10f); i++)
            {
                match.Tick();
            }

            Assert.AreEqual(20f + match.Settings.morale.moraleRegen * 10f, squad.Morale, 0.05f, "В спокойствии — прежняя быстрая скорость");
        }

        [Test]
        public void Reinforcement_RaisesMorale()
        {
            var match = new Match(1, TestSettings.Create());
            match.Settings.morale.reinforceMoraleGain = 3f;
            Village village = match.AddVillage(float2.zero);
            village.Owner = Side.France;
            Squad squad = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line, 30);
            squad.Morale = 40f;
            squad.TimeSinceLoss = 0f;

            float before = 0f;
            for (int i = 0; i < TicksFor(10f); i++)
            {
                before = squad.Morale;
                match.Tick();
                if (match.LastTickReinforcements.Count > 0)
                {
                    float regen = match.Settings.morale.moraleRegen * SimulationTime.TickDuration;
                    Assert.AreEqual(before + 3f, squad.Morale, regen + 1e-3f, "Пришёл человек — +reinforceMoraleGain");
                    return;
                }
            }

            Assert.Fail("Пополнения не было");
        }

        [Test]
        public void MeleeKills_RaiseMoraleOfOpponent()
        {
            float AttackerMorale(float killGain)
            {
                Match match = CombatTestScenarios.FacingLines(1, 0.2f, out Squad attacker, out _);
                match.Settings.morale.killMoraleGain = killGain;
                for (int i = 0; i < TicksFor(5f); i++)
                {
                    match.Tick();
                }

                Assert.IsTrue(attacker.IsInMelee, "Сошлись в рукопашную");
                return attacker.Morale;
            }

            // Тот же seed — те же потери; разница в духе — только от прибавки за убитых.
            Assert.Greater(AttackerMorale(0.8f), AttackerMorale(0f), "Убитые в рукопашной ободряют противника");
        }
    }
}
