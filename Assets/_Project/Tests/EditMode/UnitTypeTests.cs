using System.Collections.Generic;
using System.Linq;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Morale;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class UnitTypeTests
    {
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
            Squad fusiliers = match.AddSquad(Side.France, new float2(20f, 0f), 90f, Formation.Line, UnitType.Fusiliers);
            Assert.AreEqual(16, fusiliers.Alive);
            Assert.AreEqual(UnitType.Fusiliers, fusiliers.Type);
        }

        [Test]
        public void Grenadiers_CostMore_AndHireSlower_ThanFusiliers()
        {
            SimulationSettings settings = TestSettings.Create();
            settings.buildings.grenadierHireCost = 5;
            settings.buildings.grenadierHireInterval = 4f;
            var match = new Match(1, settings);
            match.AddHeadquarters(Side.France, new float2(-30f, 0f));
            match.GetPlayer(Side.France).Gold = 1000;
            match.Enqueue(new BuildBarracksCommand(Side.France, new float2(-24f, 0f)));
            match.Tick();
            Building barracks = match.Buildings.Single(b => b.Type == BuildingType.Barracks);
            barracks.BuildRemaining = 0f;
            match.Enqueue(new SetHiringCommand(barracks.Id, UnitType.Grenadiers, true));
            match.Tick();
            int goldBefore = match.GetPlayer(Side.France).Gold;

            RunTicks(match, 40);
            PlayerState player = match.GetPlayer(Side.France);
            Assert.AreEqual(1, player.GrenadierReserve, "Гренадёр — раз в 4 с");
            Assert.AreEqual(goldBefore - 5, player.Gold, "Гренадёр стоит 5");

            match.Enqueue(new SetHiringCommand(barracks.Id, UnitType.Grenadiers, false));
            match.Enqueue(new SetHiringCommand(barracks.Id, UnitType.Fusiliers, true));
            match.Tick();
            Assert.AreEqual(2, BuildRules.GetHireCost(match, barracks), "Фузилёр стоит 2");
            RunTicks(match, 40);
            Assert.AreEqual(2, player.FusilierReserve, "Фузилёр — раз в 2 с");
        }

        [Test]
        public void BothInfantryTypes_AreHiredInTurn_EachAtItsOwnPace()
        {
            SimulationSettings settings = TestSettings.Create();
            settings.buildings.grenadierHireInterval = 4f;
            var match = new Match(1, settings);
            match.AddHeadquarters(Side.France, new float2(-30f, 0f));
            match.GetPlayer(Side.France).Gold = 1000;
            match.Enqueue(new BuildBarracksCommand(Side.France, new float2(-24f, 0f)));
            match.Tick();
            Building barracks = match.Buildings.Single(b => b.Type == BuildingType.Barracks);
            barracks.BuildRemaining = 0f;
            match.Enqueue(new SetHiringCommand(barracks.Id, UnitType.Fusiliers, true));
            match.Enqueue(new SetHiringCommand(barracks.Id, UnitType.Grenadiers, true));
            PlayerState player = match.GetPlayer(Side.France);

            // Записываем, кто и на каком тике пришёл: по одному, по очереди, гренадёр — вдвое дольше.
            var hires = new List<(int Tick, UnitType Type)>();
            int fusiliers = 0, grenadiers = 0;
            for (int tick = 0; tick < 200 && hires.Count < 4; tick++)
            {
                match.Tick();
                if (player.FusilierReserve > fusiliers)
                {
                    hires.Add((tick, UnitType.Fusiliers));
                }

                if (player.GrenadierReserve > grenadiers)
                {
                    hires.Add((tick, UnitType.Grenadiers));
                }

                fusiliers = player.FusilierReserve;
                grenadiers = player.GrenadierReserve;
            }

            Assert.AreEqual(UnitType.Fusiliers, hires[0].Type, "Первым — фузилёр");
            Assert.AreEqual(UnitType.Grenadiers, hires[1].Type);
            Assert.AreEqual(UnitType.Fusiliers, hires[2].Type);
            Assert.AreEqual(UnitType.Grenadiers, hires[3].Type);
            Assert.AreEqual(40, hires[1].Tick - hires[0].Tick, "Гренадёр — 4 с после фузилёра");
            Assert.AreEqual(20, hires[2].Tick - hires[1].Tick, "Фузилёр — 2 с после гренадёра");
        }

        [Test]
        public void Barracks_HiresFusiliersFirst()
        {
            var match = new Match(1, TestSettings.Create());
            match.AddHeadquarters(Side.France, new float2(-30f, 0f));
            match.GetPlayer(Side.France).Gold = 1000;
            match.Enqueue(new BuildBarracksCommand(Side.France, new float2(-24f, 0f)));
            match.Tick();
            Building barracks = match.Buildings.Single(b => b.Type == BuildingType.Barracks);
            barracks.BuildRemaining = 0f;
            match.Enqueue(new SetHiringCommand(barracks.Id, UnitType.Grenadiers, true));
            match.Enqueue(new SetHiringCommand(barracks.Id, UnitType.Fusiliers, true));

            RunTicks(match, 21);

            Assert.AreEqual(1, match.GetPlayer(Side.France).FusilierReserve);
            Assert.AreEqual(0, match.GetPlayer(Side.France).GrenadierReserve);
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
        public void Fusiliers_StaySquadWithFewerMen()
        {
            var match = new Match(1, TestSettings.Create());
            Squad enough = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line, UnitType.Fusiliers, 6);
            Squad tooFew = match.AddSquad(Side.France, new float2(30f, 0f), 90f, Formation.Line, UnitType.Fusiliers, 5);

            match.Tick();

            Assert.IsFalse(enough.IsRouting, "У фузилёров (в тестовых настройках) отряд — от 6 человек");
            Assert.IsTrue(tooFew.IsRouting);
        }
    }
}
