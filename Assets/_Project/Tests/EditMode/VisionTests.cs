using Marchlands.Simulation.AI;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;
using System.Linq;

namespace Marchlands.Tests.EditMode
{
    /// <summary>Туман войны: чужие отряды видны только в обзоре своих отрядов, зданий и деревень; шар снимает туман.</summary>
    public class VisionTests
    {
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
        public void WithoutFog_EveryoneIsVisible()
        {
            var match = new Match(1, TestSettings.Create());
            match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            Squad far = match.AddSquad(Side.Austria, new float2(200f, 0f), 270f, Formation.Line);

            match.Tick();

            Assert.IsTrue(match.IsVisible(far, Side.France));
        }

        [Test]
        public void Fog_EnemySquad_VisibleOnlyWithinVisionRange()
        {
            var match = new Match(1, TestSettings.Create());
            Squad own = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            Squad near = match.AddSquad(Side.Austria, new float2(0f, 24f), 270f, Formation.Line);
            Squad far = match.AddSquad(Side.Austria, new float2(0f, -27f), 90f, Formation.Line);
            match.EnableFogOfWar();

            Assert.IsTrue(match.IsVisible(near, Side.France), "ближе обзора пехоты (25) — виден");
            Assert.IsFalse(match.IsVisible(far, Side.France), "дальше обзора — в тумане");
            Assert.IsTrue(match.IsVisible(own, Side.France), "свои видны всегда");
            Assert.IsTrue(match.IsVisible(own, Side.Austria), "у австрийцев отряд рядом — француз виден и им");
        }

        [Test]
        public void Fog_CavalrySeesFartherThanInfantry()
        {
            var match = new Match(1, TestSettings.Create());
            match.AddSquad(Side.France, float2.zero, 90f, Formation.Line, UnitType.Cavalry);
            Squad enemy = match.AddSquad(Side.Austria, new float2(28f, 0f), 270f, Formation.Line);
            match.EnableFogOfWar();

            Assert.IsTrue(match.IsVisible(enemy, Side.France));
            Assert.IsFalse(match.IsVisible(match.Squads[0], Side.Austria), "пехота на 28 конницу не видит");
        }

        [Test]
        public void Fog_OwnVillageAndBuilding_GiveVision()
        {
            var match = new Match(1, TestSettings.Create());
            Village village = match.AddVillage(new float2(100f, 0f));
            village.Owner = Side.France;
            match.AddHeadquarters(Side.France, new float2(-100f, 0f));
            Squad atVillage = match.AddSquad(Side.Austria, new float2(110f, 0f), 270f, Formation.Line);
            Squad atHq = match.AddSquad(Side.Austria, new float2(-85f, 0f), 270f, Formation.Line);
            Squad nowhere = match.AddSquad(Side.Austria, new float2(0f, 0f), 270f, Formation.Line);
            match.EnableFogOfWar();

            Assert.IsTrue(match.IsVisible(atVillage, Side.France), "в обзоре своей деревни");
            Assert.IsTrue(match.IsVisible(atHq, Side.France), "в обзоре своего штаба");
            Assert.IsFalse(match.IsVisible(nowhere, Side.France));
        }

        [Test]
        public void Fog_SquadComingIntoRange_BecomesVisibleAfterTick()
        {
            var match = new Match(1, TestSettings.Create());
            match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            Squad enemy = match.AddSquad(Side.Austria, new float2(0f, 40f), 270f, Formation.Line);
            match.EnableFogOfWar();
            Assert.IsFalse(match.IsVisible(enemy, Side.France));

            match.Enqueue(new MoveCommand(enemy.Id, new float2(0f, 10f), 270f));
            for (int i = 0; i < TicksFor(60f) && !match.IsVisible(enemy, Side.France); i++)
            {
                match.Tick();
            }

            Assert.IsTrue(match.IsVisible(enemy, Side.France));
            Assert.LessOrEqual(math.length(enemy.Position), match.Settings.vision.infantryVisionRange + 0.01f);
        }

        [Test]
        public void Balloon_CostsGold_TakesTime_ThenLiftsFog()
        {
            var match = new Match(1, TestSettings.Create());
            match.AddHeadquarters(Side.France, new float2(-100f, 0f));
            match.AddHeadquarters(Side.Austria, new float2(100f, 0f));
            Squad enemy = match.AddSquad(Side.Austria, new float2(0f, 0f), 270f, Formation.Line);
            match.EnableFogOfWar();
            PlayerState player = match.GetPlayer(Side.France);
            player.Gold = 500;

            match.Enqueue(new ResearchBalloonCommand(Side.France));
            match.Tick();
            Assert.AreEqual(100, player.Gold, "400 — сразу");
            Assert.IsTrue(player.IsResearchingBalloon);
            Assert.IsFalse(match.IsVisible(enemy, Side.France));

            RunTicks(match, TicksFor(40f) - 2);
            Assert.IsFalse(player.HasBalloon, "ещё изучают");
            Assert.IsFalse(match.IsVisible(enemy, Side.France));

            RunTicks(match, 2);
            Assert.IsTrue(player.HasBalloon);
            Assert.IsTrue(match.IsVisible(enemy, Side.France), "с шаром видно всё");
        }

        [Test]
        public void Balloon_NotTwice_NotWithoutFog_NotWithoutGold()
        {
            var match = new Match(1, TestSettings.Create());
            match.AddHeadquarters(Side.France, new float2(-100f, 0f));
            PlayerState player = match.GetPlayer(Side.France);
            player.Gold = 1000;
            Assert.IsFalse(BuildRules.CanResearchBalloon(match, Side.France), "без тумана шар не нужен");

            match.EnableFogOfWar();
            player.Gold = 399;
            Assert.IsFalse(BuildRules.CanResearchBalloon(match, Side.France), "мало золота");

            player.Gold = 1000;
            match.Enqueue(new ResearchBalloonCommand(Side.France));
            match.Enqueue(new ResearchBalloonCommand(Side.France));
            match.Tick();
            Assert.AreEqual(600, player.Gold, "второй раз не платят");
            Assert.IsFalse(BuildRules.CanResearchBalloon(match, Side.France));
        }

        [Test]
        public void Ai_DoesNotFireAtEnemyHiddenInFog()
        {
            Match match = CombatTestScenarios.FacingLines(1, 20f, out _, out Squad ai);
            match.Settings.vision.infantryVisionRange = 5f;
            match.SetRetreatPoint(Side.Austria, new float2(0f, 60f));
            match.AddController(new TacticalAi(Side.Austria));
            match.EnableFogOfWar();

            bool fired = false;
            for (int tick = 0; tick < TicksFor(40f); tick++)
            {
                match.Tick();
                fired |= match.LastTickVolleys.Any(v => v.ShooterId == ai.Id);
            }

            Assert.IsFalse(fired, "враг в тумане — ИИ о нём не знает");
        }
    }
}
