using System.Linq;
using Marchlands.Simulation.AI;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class TacticalAiTests
    {
        private const int SkirmishTicks = 3000;

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

        /// <summary>Австрийская линия под управлением ИИ против неподвижной французской, фронты разделяет <paramref name="frontDistance"/>.</summary>
        private static Match AiAgainstDummy(ulong seed, float frontDistance, out Squad ai, out Squad dummy)
        {
            Match match = CombatTestScenarios.FacingLines(seed, frontDistance, out dummy, out ai);
            match.SetRetreatPoint(Side.Austria, new float2(0f, 60f));
            match.AddController(new TacticalAi(Side.Austria));
            return match;
        }

        [Test]
        public void Ai_NeverFiresBeyondEffectiveRange()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                Match match = AiAgainstDummy(seed, 20f, out Squad ai, out _);
                bool fired = false;
                for (int tick = 0; tick < TicksFor(40f); tick++)
                {
                    match.Tick();
                    foreach (VolleyEvent volley in match.LastTickVolleys.Where(v => v.ShooterId == ai.Id))
                    {
                        fired = true;
                        Assert.LessOrEqual(volley.Distance, match.Settings.combat.effectiveRange + 1e-3f, $"seed {seed}");
                        Assert.GreaterOrEqual(volley.Distance, match.Settings.combat.pointBlankRange - 0.5f, $"seed {seed}");
                    }
                }

                Assert.IsTrue(fired, $"seed {seed}: ИИ так и не выстрелил");
            }
        }

        [Test]
        public void Ai_VolleyMoment_IsUnpredictable()
        {
            var firstVolleyTicks = new System.Collections.Generic.List<int>();
            for (ulong seed = 1; seed <= 30; seed++)
            {
                Match match = AiAgainstDummy(seed, 5f, out Squad ai, out _);
                for (int tick = 0; tick < TicksFor(10f); tick++)
                {
                    match.Tick();
                    if (match.LastTickVolleys.Any(v => v.ShooterId == ai.Id))
                    {
                        firstVolleyTicks.Add(tick);
                        break;
                    }
                }
            }

            Assert.AreEqual(30, firstVolleyTicks.Count, "ИИ стреляет в каждом бою");
            AiSettingsCheck(firstVolleyTicks);
        }

        private static void AiSettingsCheck(System.Collections.Generic.List<int> ticks)
        {
            var settings = TestSettings.Create();
            int earliest = TicksFor(settings.ai.reactionDelay + settings.combat.aimTime + settings.ai.volleyDelayMin);
            Assert.GreaterOrEqual(ticks.Min(), earliest - 1, "Не раньше реакции и прицеливания");
            Assert.GreaterOrEqual(ticks.Distinct().Count(), 6, "Момент залпа разный от боя к бою");
            Assert.GreaterOrEqual(ticks.Max() - ticks.Min(), TicksFor((settings.ai.volleyDelayMax - settings.ai.volleyDelayMin) * 0.6f),
                "Разброс момента залпа — на большую часть volleyDelay");
        }

        /// <summary>Поле с местностью, ИИ избегает огня зданий; французский блокгауз посреди пути.</summary>
        private static Match FieldWithBlockhouse(out Squad ai, out Marchlands.Simulation.Buildings.Building blockhouse)
        {
            var settings = TestSettings.Create();
            settings.ai.buildingDangerCost = 4f;
            settings.ai.buildingDangerMargin = 2f;
            var match = new Match(1, settings);
            match.SetTerrain(new Marchlands.Simulation.Terrain.TerrainMap(new float2(-80f, -40f), new float2(80f, 40f)));
            match.AddHeadquarters(Side.Austria, new float2(70f, 0f));
            blockhouse = match.AddBlockhouse(Side.France, new float2(0f, 0f));
            ai = match.AddSquad(Side.Austria, new float2(40f, 0f), 180f, Formation.Line);
            match.AddController(new TacticalAi(Side.Austria));
            return match;
        }

        [Test]
        public void Ai_GoesAroundBlockhouse_InsteadOfMarchingPastIt()
        {
            Match match = FieldWithBlockhouse(out Squad ai, out var blockhouse);
            Village village = match.AddVillage(new float2(-40f, 0f));
            float range = Marchlands.Simulation.Buildings.Volley.Of(blockhouse, match.Settings.buildings).Range;

            float closest = float.MaxValue;
            for (int i = 0; i < TicksFor(200f) && village.Owner != Side.Austria; i++)
            {
                match.Tick();
                closest = math.min(closest, math.distance(ai.Position, blockhouse.Position));
            }

            Assert.AreEqual(Side.Austria, village.Owner, "Деревня взята");
            Assert.Greater(closest, range, "Обошёл блокгауз, не подставляясь под его огонь");
            Assert.AreEqual(UnitStats.Of(ai, match.Settings).squadSize, ai.Alive, "Без потерь");
        }

        [Test]
        public void Ai_PrefersVillageOutOfBlockhouseFire()
        {
            Match match = FieldWithBlockhouse(out Squad ai, out _);
            // Ближняя деревня — под самым блокгаузом, дальняя — в стороне.
            Village covered = match.AddVillage(new float2(6f, 0f));
            Village safe = match.AddVillage(new float2(30f, 35f));
            RunTicks(match, TicksFor(3f));

            Assert.AreEqual(SquadOrderType.Move, ai.Order.Type);
            Assert.Greater(math.distance(ai.Order.Target, covered.Position), 10f, "Не идёт под огонь");
            RunTicks(match, TicksFor(60f));
            Assert.AreEqual(Side.Austria, safe.Owner, "Взял деревню в стороне");
        }

        [Test]
        public void Ai_CapturesNearbyVillage_WhenNoEnemyClose_EvenIfThreatElsewhere()
        {
            var match = new Match(1, TestSettings.Create());
            match.AddHeadquarters(Side.Austria, new float2(0f, 40f));
            Village village = match.AddVillage(new float2(8f, 0f));
            Squad ai = match.AddSquad(Side.Austria, float2.zero, 0f, Formation.Column);
            // Угроза у штаба ИИ, но далеко от отряда и деревни.
            match.AddSquad(Side.France, new float2(0f, 55f), -90f, Formation.Line);
            match.AddController(new TacticalAi(Side.Austria));

            RunTicks(match, TicksFor(3f));
            Assert.AreEqual(SquadOrderType.Move, ai.Order.Type, "Идёт к деревне");
            Assert.Less(math.distance(ai.Order.Target, village.Position), 0.5f);

            RunTicks(match, TicksFor(30f));
            Assert.AreEqual(Side.Austria, village.Owner, "Деревня взята");
        }

        [Test]
        public void Ai_ChargesReloadingEnemyNearby()
        {
            Match match = AiAgainstDummy(1, 5f, out Squad ai, out Squad dummy);
            dummy.FireState = FireState.Reloading;
            dummy.FireTimer = 12f;

            RunTicks(match, TicksFor(0.8f) + 2);

            Assert.AreEqual(SquadOrderType.Charge, ai.Order.Type);
            Assert.AreEqual(dummy.Id, ai.Order.TargetSquadId);
        }

        [Test]
        public void Ai_DoesNotCharge_WhenEnemyIsLoaded()
        {
            Match match = AiAgainstDummy(1, 5f, out Squad ai, out _);

            RunTicks(match, TicksFor(3f));

            Assert.AreNotEqual(SquadOrderType.Charge, ai.Order.Type);
            Assert.IsFalse(ai.IsInMelee);
        }

        [Test]
        public void Ai_DoesNotCharge_WithLowMorale()
        {
            Match match = AiAgainstDummy(1, 5f, out Squad ai, out Squad dummy);
            dummy.FireState = FireState.Reloading;
            dummy.FireTimer = 12f;
            ai.Morale = 55f;

            RunTicks(match, TicksFor(3f));

            Assert.AreNotEqual(SquadOrderType.Charge, ai.Order.Type);
            Assert.IsFalse(ai.IsInMelee);
        }

        [Test]
        public void Ai_DoesNotCharge_FromBeyondChargeDistance()
        {
            Match match = AiAgainstDummy(1, 11f, out Squad ai, out Squad dummy);
            dummy.FireState = FireState.Reloading;
            dummy.FireTimer = 30f;

            // Пока ИИ ещё дальше 8 u, он подходит, а не бежит в штыки.
            for (int tick = 0; tick < TicksFor(2f); tick++)
            {
                match.Tick();
                if (FireGeometry.Distance(ai, dummy, match.Settings.squads) >= 8.5f)
                {
                    Assert.AreNotEqual(SquadOrderType.Charge, ai.Order.Type, $"Тик {tick}");
                }
            }
        }

        [Test]
        public void Ai_Retreats_WhenMoraleIsLow_AndReturnsWhenRecovered()
        {
            Match match = AiAgainstDummy(1, 9f, out Squad ai, out _);
            ai.Morale = 35f;

            RunTicks(match, TicksFor(2f));
            Assert.AreEqual(SquadOrderType.Move, ai.Order.Type);
            Assert.AreEqual(60f, ai.Order.Target.y, 1e-3f, "Отход к точке отхода стороны");

            ai.Morale = 75f;
            RunTicks(match, TicksFor(2f));
            Assert.Less(ai.Order.Target.y, 60f, "С моралью выше aiReturnMorale отряд снова наступает");
        }

        [Test]
        public void Ai_OrdersArriveAfterReactionDelay()
        {
            Match match = AiAgainstDummy(1, 20f, out Squad ai, out _);
            int delay = TicksFor(match.Settings.ai.reactionDelay);

            RunTicks(match, delay);
            Assert.AreEqual(SquadOrderType.None, ai.Order.Type, "До задержки реакции приказа ещё нет");

            match.Tick();
            Assert.AreEqual(SquadOrderType.Move, ai.Order.Type);
        }

        private static Match Skirmish(ulong seed)
        {
            var match = new Match(seed, TestSettings.Create());
            match.SetRetreatPoint(Side.France, new float2(0f, -35f));
            match.SetRetreatPoint(Side.Austria, new float2(0f, 35f));
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 12f;
                match.AddSquad(Side.France, new float2(x, -12f), 90f, Formation.Line);
                match.AddSquad(Side.Austria, new float2(x, 12f), -90f, Formation.Line);
            }

            match.AddController(new TacticalAi(Side.France));
            match.AddController(new TacticalAi(Side.Austria));
            return match;
        }

        /// <summary>Тик, на котором одна из сторон разгромлена, или -1.</summary>
        private static int RunUntilRout(Match match, out Side? broken)
        {
            for (int tick = 0; tick < SkirmishTicks; tick++)
            {
                match.Tick();
                if (match.IsSideBroken(Side.France))
                {
                    broken = Side.France;
                    return tick;
                }

                if (match.IsSideBroken(Side.Austria))
                {
                    broken = Side.Austria;
                    return tick;
                }
            }

            broken = null;
            return -1;
        }

        [Test]
        public void AiVsAi_ThreeOnThree_EndsWithRoutWithin3000Ticks([Range(1, 10)] int seed)
        {
            Match match = Skirmish((ulong)seed);

            int tick = RunUntilRout(match, out Side? broken);

            Assert.GreaterOrEqual(tick, 0, $"seed {seed}: за {SkirmishTicks} тиков ни одна сторона не разгромлена");
            Assert.IsNotNull(broken);
        }

        [Test]
        public void AiVsAi_SameSeed_GivesSameOutcome()
        {
            for (ulong seed = 1; seed <= 3; seed++)
            {
                Match first = Skirmish(seed);
                Match second = Skirmish(seed);

                int firstTick = RunUntilRout(first, out Side? firstBroken);
                int secondTick = RunUntilRout(second, out Side? secondBroken);

                Assert.AreEqual(firstTick, secondTick, $"seed {seed}");
                Assert.AreEqual(firstBroken, secondBroken, $"seed {seed}");
                CollectionAssert.AreEqual(
                    first.Squads.Select(s => (s.Id, s.Alive, s.Position)).ToArray(),
                    second.Squads.Select(s => (s.Id, s.Alive, s.Position)).ToArray(),
                    $"seed {seed}");
            }
        }
    }
}
