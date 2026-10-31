using System.Collections.Generic;
using Marchlands.Simulation.AI;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Morale;
using Marchlands.Simulation.Squads;
using Marchlands.Simulation.Terrain;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    /// <summary>
    /// Марш и огонь: залп по бегущим и из колонны, отложенный марш, поворот колонны на ходу,
    /// бегство по дорогам, заход конницы ИИ во фланг.
    /// </summary>
    public class MarchFireTests
    {
        private static void Run(Match match, float seconds)
        {
            int ticks = (int)math.round(seconds * SimulationTime.TicksPerSecond);
            for (int i = 0; i < ticks; i++)
            {
                match.Tick();
            }
        }

        [Test]
        public void Volley_AtRoutingEnemy_IsAllowed()
        {
            Match match = CombatTestScenarios.FacingLines(1, 5f, out Squad shooter, out Squad target);
            MoraleSystem.Rout(match, target);
            Assert.IsTrue(target.IsRouting);
            Assert.IsTrue(VolleyRules.CanVolley(match, shooter), "По бегущим можно стрелять");
            Assert.GreaterOrEqual(CombatTestScenarios.FireOnce(match, shooter), 0);
        }

        [Test]
        public void MoveOrder_ContinuesAfterVolley()
        {
            var match = new Match(1, TestSettings.Create());
            Squad squad = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            Squad enemy = match.AddSquad(Side.Austria, new float2(4f, 8f), -90f, Formation.Line);
            match.Enqueue(new MoveCommand(squad.Id, new float2(0f, 30f), 90f));
            match.Tick();
            CombatTestScenarios.FireOnce(match, squad);
            MoraleSystem.Rout(match, enemy);
            for (int i = 0; i < 600 && squad.Order.Type == SquadOrderType.Move; i++)
            {
                match.Tick();
            }

            Assert.AreEqual(SquadOrderType.None, squad.Order.Type);
            Assert.Less(math.distance(squad.Position, new float2(0f, 30f)), 0.2f, "Дошёл после залпа");
        }

        [Test]
        public void FaceDuringMarch_SuspendsMarch_UntilEnemiesAreGone()
        {
            var match = new Match(1, TestSettings.Create());
            match.SetRetreatPoint(Side.Austria, new float2(200f, 0f));
            Squad squad = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            Squad enemy = match.AddSquad(Side.Austria, new float2(10f, 2f), 180f, Formation.Line);
            var goal = new float2(0f, 40f);
            match.Enqueue(new MoveCommand(squad.Id, goal, 90f));
            match.Tick();

            // Развернули к врагу — встал, марш отложен.
            match.Enqueue(new FaceCommand(squad.Id, 0f));
            Run(match, 5f);
            Assert.AreEqual(SquadOrderType.None, squad.Order.Type);
            Assert.IsTrue(squad.IsMarchSuspended, "Марш не отменён, а отложен");
            float2 standing = squad.Position;
            Run(match, 5f);
            Assert.Less(math.distance(standing, squad.Position), 0.01f, "Пока враг рядом — стоит");

            // Враг бежит — отряд сам продолжает марш.
            MoraleSystem.Rout(match, enemy);
            for (int i = 0; i < 1000 && math.distance(squad.Position, goal) > 0.1f; i++)
            {
                match.Tick();
            }

            Assert.Less(math.distance(squad.Position, goal), 0.1f, "Дошёл до цели марша");
            Assert.IsFalse(squad.IsMarchSuspended);
        }

        [Test]
        public void NewMoveOrder_DropsSuspendedMarch()
        {
            var match = new Match(1, TestSettings.Create());
            Squad squad = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            match.AddSquad(Side.Austria, new float2(10f, 2f), 180f, Formation.Line);
            match.Enqueue(new MoveCommand(squad.Id, new float2(0f, 40f), 90f));
            match.Tick();
            match.Enqueue(new FaceCommand(squad.Id, 0f));
            match.Tick();
            Assert.IsTrue(squad.IsMarchSuspended);

            match.Enqueue(new StopCommand(squad.Id));
            match.Tick();
            Assert.IsFalse(squad.IsMarchSuspended, "Стоп отменяет и отложенный марш");
        }

        [Test]
        public void Column_TurnsOnTheMove()
        {
            var match = new Match(1, TestSettings.Create());
            Squad squad = match.AddSquad(Side.France, float2.zero, 90f, Formation.Column);
            var goal = new float2(10f, 20f);
            match.Enqueue(new MoveCommand(squad.Id, goal, Angles.Of(goal)));
            match.Tick();
            for (int i = 0; i < 400 && squad.Order.Type == SquadOrderType.Move; i++)
            {
                float2 before = squad.Position;
                match.Tick();
                if (squad.Order.Type == SquadOrderType.Move)
                {
                    Assert.Greater(math.distance(before, squad.Position), 0f, "Колонна не стоит на месте, поворачивая");
                }
            }

            Assert.Less(math.distance(squad.Position, goal), 0.1f);
        }

        [Test]
        public void Line_StillTurnsInPlace()
        {
            var match = new Match(1, TestSettings.Create());
            Squad squad = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            match.Enqueue(new MoveCommand(squad.Id, new float2(10f, 20f), 0f));
            match.Tick();
            match.Tick();
            Assert.Less(math.length(squad.Position), 1e-4f, "Линия сначала разворачивается на месте");
        }

        [Test]
        public void ColumnVolley_AllRanksFire_ButWeakerThanLine()
        {
            int lineKills = 0;
            int columnKills = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                lineKills += VolleyFrom(seed, Formation.Line, out int lineShooters);
                columnKills += VolleyFrom(seed, Formation.Column, out int columnShooters);
                Assert.AreEqual(30, lineShooters);
                Assert.AreEqual(30, columnShooters, "В колонне стреляют все");
            }

            Assert.Less(columnKills, lineKills, "Колонна бьёт хуже линии");
            // Одна первая шеренга колонны (6 из 30) убила бы примерно пятую часть от линии.
            Assert.Greater(columnKills, lineKills / 3, "…но заметно лучше одной первой шеренги");
        }

        /// <summary>Залп отряда в 30 человек в строю <paramref name="formation"/>; фронт стрелка в 5 u от свежей линии цели.</summary>
        private static int VolleyFrom(ulong seed, Formation formation, out int shooters)
        {
            var match = new Match(seed, TestSettings.Create());
            float halfDepth = FormationLayout.GetHalfExtents(formation, 30, match.Settings.squads).y;
            Squad shooter = match.AddSquad(Side.France, new float2(0f, -halfDepth), 90f, formation, 30);
            float targetHalf = FormationLayout.GetHalfExtents(Formation.Line, 30, match.Settings.squads).y;
            match.AddSquad(Side.Austria, new float2(0f, 5f + targetHalf), -90f, Formation.Line, 30);
            match.Enqueue(new VolleyCommand(shooter.Id));
            for (int i = 0; i < 100; i++)
            {
                match.Tick();
                foreach (VolleyEvent volley in match.LastTickVolleys)
                {
                    if (volley.ShooterId == shooter.Id)
                    {
                        shooters = volley.Shooters;
                        return volley.Casualties;
                    }
                }
            }

            Assert.Fail("Залпа не было");
            shooters = 0;
            return 0;
        }

        [Test]
        public void Routing_PrefersRoad_AndNeverEntersRiver()
        {
            var match = new Match(1, TestSettings.Create());
            var map = new TerrainMap(new float2(-60f, -30f), new float2(60f, 30f));
            map.AddRoad(new List<float2> { new float2(-60f, 0f), new float2(60f, 0f) });
            map.AddRiver(new List<float2> { new float2(20f, -40f), new float2(20f, 40f) }, 2f);
            map.AddCrossing(new float2(20f, 0f), CrossingType.Bridge);
            match.SetTerrain(map);
            match.SetRetreatPoint(Side.France, new float2(45f, 6f));
            Squad squad = match.AddSquad(Side.France, new float2(-40f, 6f), 0f, Formation.Line);
            MoraleSystem.Rout(match, squad);

            bool onRoad = false;
            for (int i = 0; i < 1500 && !squad.IsWithdrawn && match.FindSquad(squad.Id) != null; i++)
            {
                match.Tick();
                Assert.IsFalse(map.IsWater(squad.Position), "Бегущие не лезут в реку");
                onRoad |= map.IsOnRoad(squad.Position, match.Settings.terrain.roadHalfWidth);
            }

            Assert.IsTrue(onRoad, "Бежали по дороге");
            Assert.IsNull(match.FindSquad(squad.Id), "Добежали до своих");
        }

        /// <summary>Поле 100×60, река по x = 0, мост на y = 0 по дороге с запада на восток.</summary>
        private static Match BridgeMap()
        {
            var match = new Match(1, TestSettings.Create());
            var map = new TerrainMap(new float2(-50f, -30f), new float2(50f, 30f));
            map.AddRoad(new List<float2> { new float2(-50f, 0f), new float2(50f, 0f) });
            map.AddRiver(new List<float2> { new float2(0f, -40f), new float2(0f, 40f) }, 2f);
            map.AddCrossing(new float2(0f, 0f), CrossingType.Bridge);
            match.SetTerrain(map);
            return match;
        }

        [Test]
        public void Line_SqueezesOnBridge_AndWidensAfter()
        {
            Match match = BridgeMap();
            Squad squad = match.AddSquad(Side.France, new float2(-12f, 0f), 0f, Formation.Line);
            match.Enqueue(new MoveCommand(squad.Id, new float2(12f, 0f), 0f));
            bool squeezed = false;
            for (int i = 0; i < 600 && squad.Order.Type != SquadOrderType.None || i == 0; i++)
            {
                match.Tick();
                if (match.Terrain.IsOnBridge(squad.Position))
                {
                    squeezed = true;
                    Assert.Greater(squad.FileLimit, 0, "На мосту строй сжат");
                    float half = FormationLayout.GetHalfExtents(squad, match.Settings.squads).x;
                    Assert.LessOrEqual(half, match.Settings.terrain.bridgeHalfWidth, "Не шире настила");
                }
            }

            Assert.IsTrue(squeezed, "Прошёл по мосту");
            Assert.AreEqual(Formation.Line, squad.Formation, "Линия не отменяется");
            Assert.AreEqual(0, squad.FileLimit, "За мостом раздался обратно");
        }

        [TestCase(-14f, -12f, 12f, 10f)]
        [TestCase(-10f, 14f, 14f, -12f)]
        [TestCase(-12f, -16f, 8f, 16f)]
        public void Line_ApproachingBridgeAtAngle_NoSoldierOverWater(float fromX, float fromY, float toX, float toY)
        {
            Match match = BridgeMap();
            var from = new float2(fromX, fromY);
            var to = new float2(toX, toY);
            Squad squad = match.AddSquad(Side.France, from, Angles.Of(to - from), Formation.Line);
            match.Enqueue(new MoveCommand(squad.Id, to, Angles.Of(to - from)));
            var slots = new List<float2>();
            bool crossed = false;
            for (int i = 0; i < 1500 && (squad.Order.Type != SquadOrderType.None || i == 0); i++)
            {
                match.Tick();
                FormationLayout.GetLocalSlots(squad.Formation, squad.Alive, match.Settings.squads, slots, squad.FileLimit);
                foreach (float2 slot in slots)
                {
                    float2 world = FormationLayout.LocalToWorld(slot, squad.Position, squad.Facing);
                    Assert.IsFalse(match.Terrain.IsWater(world), $"Солдат над водой на тике {i}: {world}");
                }

                crossed |= squad.Position.x > 0f;
            }

            Assert.IsTrue(crossed, "Перешёл реку");
            Assert.AreEqual(SquadOrderType.None, squad.Order.Type, "Дошёл до цели");
            Assert.Less(math.distance(squad.Position, to), 1f, "Встал в указанной точке");
        }

        [TestCase(6f, -2.8f, 75f)]
        [TestCase(6f, 2.8f, -75f)]
        [TestCase(5f, -3.5f, 90f)]
        [TestCase(8f, -3f, 150f)]
        public void Line_BesideBridgehead_CrossesWithoutSpinning(float fromX, float fromY, float facing)
        {
            Match match = BridgeMap();
            Squad squad = match.AddSquad(Side.France, new float2(fromX, fromY), facing, Formation.Line);
            var to = new float2(-25f, 0.6f);
            match.Enqueue(new MoveCommand(squad.Id, to, 180f));
            for (int i = 0; i < 1500 && (i == 0 || squad.Order.Type != SquadOrderType.None); i++)
            {
                match.Tick();
            }

            Assert.AreEqual(SquadOrderType.None, squad.Order.Type, $"Перешёл мост, а не крутится у въезда: {squad.Position}");
            Assert.Less(math.distance(squad.Position, to), 1f);
        }

        [Test]
        public void WideLine_WadesThroughFord_WithoutStalling()
        {
            var match = new Match(1, TestSettings.Create());
            var map = new TerrainMap(new float2(-50f, -30f), new float2(50f, 30f));
            map.AddRiver(new List<float2> { new float2(0f, -40f), new float2(0f, 40f) }, 2f);
            map.AddCrossing(new float2(0f, 0f), CrossingType.Ford);
            match.SetTerrain(map);
            Squad squad = match.AddSquad(Side.France, new float2(-16f, -6f), 0f, Formation.Line, 40);
            var to = new float2(14f, 5f);
            match.Enqueue(new MoveCommand(squad.Id, to, 0f));
            for (int i = 0; i < 2000 && (i == 0 || squad.Order.Type != SquadOrderType.None); i++)
            {
                match.Tick();
            }

            Assert.AreEqual(SquadOrderType.None, squad.Order.Type, "Перешёл брод, а не топчется на берегу");
            Assert.Less(math.distance(squad.Position, to), 1f);
        }

        [TestCase(0f, -20f)]
        [TestCase(-10f, -12f)]
        [TestCase(12f, -16f)]
        public void Line_AtMapEdge_TurnsToFinalFacing_AndStops(float fromX, float fromY)
        {
            var match = new Match(1, TestSettings.Create());
            match.SetTerrain(new TerrainMap(new float2(-50f, -30f), new float2(50f, 30f)));
            Squad squad = match.AddSquad(Side.France, new float2(fromX, fromY), -90f, Formation.Line, 40);
            // Точка у самого края, фронт — вдоль края: линия разворачивается у границы.
            match.Enqueue(new MoveCommand(squad.Id, new float2(0f, -29f), 0f));
            for (int i = 0; i < 1000 && (i == 0 || squad.Order.Type != SquadOrderType.None); i++)
            {
                match.Tick();
            }

            Assert.AreEqual(SquadOrderType.None, squad.Order.Type, "Развернулся и встал, а не крутится у края");
            Assert.AreEqual(0f, squad.Facing, 1f);
        }

        [Test]
        public void Line_OrderedOntoBridge_StopsAlongDeck()
        {
            Match match = BridgeMap();
            Squad squad = match.AddSquad(Side.France, new float2(-14f, -6f), 0f, Formation.Line);
            match.Enqueue(new MoveCommand(squad.Id, new float2(0f, 0.5f), 90f));
            for (int i = 0; i < 1500 && (squad.Order.Type != SquadOrderType.None || i == 0); i++)
            {
                match.Tick();
            }

            Assert.AreEqual(SquadOrderType.None, squad.Order.Type, "Приказ выполнен, а не застрял");
            Assert.Greater(squad.FileLimit, 0, "На мосту сжат");
            Assert.Less(math.min(math.abs(Angles.Delta(squad.Facing, 0f)), 180f - math.abs(Angles.Delta(squad.Facing, 0f))), 1f, "Стоит вдоль настила, а не поперёк");
        }

        [Test]
        public void SqueezedLine_FiresWeakerThanFullLine()
        {
            int full = 0;
            int squeezed = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                full += SqueezeVolley(seed, 0);
                squeezed += SqueezeVolley(seed, 6);
            }

            Assert.Less(squeezed, full, "Задние ряды сжатого строя дальше от врага");
        }

        private static int SqueezeVolley(ulong seed, int fileLimit)
        {
            var match = new Match(seed, TestSettings.Create());
            Squad shooter = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line, 30);
            shooter.FileLimit = fileLimit;
            float shooterHalf = FormationLayout.GetHalfExtents(shooter, match.Settings.squads).y;
            shooter.Position = new float2(0f, -shooterHalf);
            float targetHalf = FormationLayout.GetHalfExtents(Formation.Line, 30, match.Settings.squads).y;
            match.AddSquad(Side.Austria, new float2(0f, 5f + targetHalf), -90f, Formation.Line, 30);
            match.Enqueue(new VolleyCommand(shooter.Id));
            for (int i = 0; i < 100; i++)
            {
                // Сжатие держим вручную: моста здесь нет.
                shooter.FileLimit = fileLimit;
                match.Tick();
                foreach (VolleyEvent volley in match.LastTickVolleys)
                {
                    if (volley.ShooterId == shooter.Id)
                    {
                        return volley.Casualties;
                    }
                }
            }

            Assert.Fail("Залпа не было");
            return 0;
        }

        [Test]
        public void AiCavalry_ChargesOnlyFromFlankOrRear()
        {
            var match = new Match(3, TestSettings.Create());
            Squad infantry = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            Squad cavalry = match.AddSquad(Side.Austria, new float2(0f, 30f), -90f, Formation.Line, UnitType.Cavalry);
            match.AddController(new TacticalAi(Side.Austria));

            bool charged = false;
            for (int i = 0; i < 1200 && !charged; i++)
            {
                match.Tick();
                if (cavalry.Order.Type == SquadOrderType.Charge)
                {
                    charged = true;
                    Assert.IsTrue(MoraleMath.IsFlank(infantry, cavalry.Position, match.Settings.morale), "Атака не в лоб");
                }
            }

            Assert.IsTrue(charged, "Конница атаковала");
        }

        [Test]
        public void Ai_FiresAtRoutingEnemy()
        {
            var match = new Match(1, TestSettings.Create());
            match.SetRetreatPoint(Side.France, new float2(0f, -1000f));
            Squad enemy = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            Squad ai = match.AddSquad(Side.Austria, new float2(0f, 5f), -90f, Formation.Line);
            match.AddController(new TacticalAi(Side.Austria));
            MoraleSystem.Rout(match, enemy);

            bool fired = false;
            for (int i = 0; i < 50 && !fired; i++)
            {
                match.Tick();
                foreach (VolleyEvent volley in match.LastTickVolleys)
                {
                    fired |= volley.ShooterId == ai.Id && volley.TargetId == enemy.Id;
                }
            }

            Assert.IsTrue(fired, "ИИ дал залп по бегущим");
        }

        [Test]
        public void Ai_FiresAtChargingEnemy_BeforeContact()
        {
            var match = new Match(2, TestSettings.Create());
            Squad attacker = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            Squad ai = match.AddSquad(Side.Austria, new float2(0f, 8f), -90f, Formation.Line);
            match.AddController(new TacticalAi(Side.Austria));
            match.Enqueue(new ChargeCommand(attacker.Id, ai.Id));

            bool fired = false;
            for (int i = 0; i < 60 && !fired; i++)
            {
                match.Tick();
                foreach (VolleyEvent volley in match.LastTickVolleys)
                {
                    fired |= volley.ShooterId == ai.Id;
                }
            }

            Assert.IsTrue(fired, "ИИ дал залп по бегущим на него в штыки");
        }

        [Test]
        public void Cavalry_IgnoresFormationChange()
        {
            var match = new Match(1, TestSettings.Create());
            Squad cavalry = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line, UnitType.Cavalry);
            match.Enqueue(new ChangeFormationCommand(cavalry.Id, Formation.Column));
            match.Tick();
            Assert.AreEqual(Formation.Line, cavalry.TargetFormation);
        }
    }
}
