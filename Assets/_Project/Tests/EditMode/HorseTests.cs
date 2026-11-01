using System.Linq;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    /// <summary>Кони: конюшня даёт 20, расширяется до 40 и 60; без коней конница не нанимается и не пополняется.</summary>
    public class HorseTests
    {
        private static readonly float2 Hq = new float2(0f, -30f);

        private static Building ReadyStable(Match match)
        {
            match.GetPlayer(Side.France).Gold = 2000;
            match.Enqueue(new BuildStableCommand(Side.France, Hq + new float2(0f, 8f)));
            match.Tick();
            Building stable = match.Buildings.Single(b => b.Type == BuildingType.Stable);
            stable.BuildRemaining = 0f;
            return stable;
        }

        private static Match CreateMatch()
        {
            var match = new Match(1, TestSettings.Create());
            match.AddHeadquarters(Side.France, Hq);
            match.AddHeadquarters(Side.Austria, new float2(0f, 60f));
            return match;
        }

        [Test]
        public void NoStable_NoHorses_StableGivesTwenty()
        {
            Match match = CreateMatch();
            Assert.AreEqual(0, BuildRules.GetHorseCapacity(match, Side.France));
            ReadyStable(match);
            Assert.AreEqual(20, BuildRules.GetHorseCapacity(match, Side.France));
            Assert.AreEqual(0, BuildRules.GetHorses(match, Side.France));
        }

        [Test]
        public void HorsesFull_CavalryHiringWaits_WithNoHorses()
        {
            Match match = CreateMatch();
            Building stable = ReadyStable(match);
            match.AddSquad(Side.France, new float2(10f, -20f), 90f, Formation.Line, UnitType.Cavalry, 20);
            match.Enqueue(new SetHiringCommand(stable.Id, UnitType.Cavalry, true));
            match.Tick();

            Assert.AreEqual(20, BuildRules.GetHorses(match, Side.France));
            Assert.AreEqual(HireWait.NoHorses, BuildRules.IsWaiting(match, stable));
            int reserveBefore = match.GetPlayer(Side.France).CavalryReserve;
            for (int i = 0; i < 300; i++)
            {
                match.Tick();
            }

            Assert.AreEqual(reserveBefore, match.GetPlayer(Side.France).CavalryReserve, "без коней всадники не приходят");
        }

        [Test]
        public void Stable_ExpandsTwice_ToFortyAndSixty_ThenStops()
        {
            Match match = CreateMatch();
            Building stable = ReadyStable(match);
            PlayerState player = match.GetPlayer(Side.France);
            int gold = player.Gold;

            match.Enqueue(new UpgradeBuildingCommand(stable.Id));
            match.Tick();
            Assert.AreEqual(gold - 150, player.Gold);
            stable.UpgradeRemaining = 0.01f;
            match.Tick();
            Assert.AreEqual(2, stable.Level);
            Assert.AreEqual(40, BuildRules.GetHorseCapacity(match, Side.France));
            Assert.AreEqual(300f, stable.MaxHealth, 1e-3f, "расширение не делает конюшню крепче");

            match.Enqueue(new UpgradeBuildingCommand(stable.Id));
            match.Tick();
            Assert.AreEqual(gold - 150 - 250, player.Gold, "второе расширение дороже");
            stable.UpgradeRemaining = 0.01f;
            match.Tick();
            Assert.AreEqual(60, BuildRules.GetHorseCapacity(match, Side.France));
            Assert.IsTrue(BuildRules.IsFullyUpgraded(match, stable));
            Assert.IsFalse(BuildRules.CanUpgrade(match, stable), "дальше 60 не расширить");
        }

        [Test]
        public void Cavalry_NotReinforcedInVillage_WithoutHorses()
        {
            Match match = CreateMatch();
            ReadyStable(match);
            Village village = match.AddVillage(new float2(20f, 0f));
            village.Owner = Side.France;
            match.AddSquad(Side.France, new float2(-20f, -20f), 90f, Formation.Line, UnitType.Cavalry, 15);
            Squad wounded = match.AddSquad(Side.France, new float2(20f, 0f), 90f, Formation.Line, UnitType.Cavalry, 5);

            for (int i = 0; i < 400; i++)
            {
                match.Tick();
            }

            Assert.AreEqual(5, wounded.Alive, "все 20 коней заняты — пополнять некем");
        }
    }
}
