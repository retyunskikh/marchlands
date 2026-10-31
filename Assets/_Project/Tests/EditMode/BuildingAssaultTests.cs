using System.Collections.Generic;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class BuildingAssaultTests
    {
        private static readonly float2 AustriaHq = new float2(30f, 0f);

        private static void RunTicks(Match match, int count)
        {
            for (int i = 0; i < count; i++)
            {
                match.Tick();
            }
        }

        private static int TicksFor(float seconds)
        {
            return (int)math.round(seconds * SimulationTime.TicksPerSecond);
        }

        [Test]
        public void Sentries_KillAboutHalfAGrenadierSquadPerMinute()
        {
            // Отряд гренадёров на эффективной дальности: 30 × 0.2 залпом раз в 22 с ≈ 16.4 в минуту; часовые — около половины.
            var settings = TestSettings.Create();
            var match = new Match(7, settings);
            Building headquarters = match.AddHeadquarters(Side.Austria, AustriaHq);
            Squad target = match.AddSquad(Side.France, AustriaHq + new float2(-6f, 0f), 0f, Formation.Line, UnitType.Grenadiers, 100000);

            var events = new List<SentryVolley>();
            const float minutes = 30f;
            int ticks = TicksFor(minutes * 60f);
            for (int i = 0; i < ticks; i++)
            {
                SentrySystem.Update(match, SimulationTime.TickDuration, events);
            }

            float killsPerMinute = (100000 - target.Alive) / minutes;
            float grenadierPerMinute = 30 * settings.combat.hitEffective * 60f / 22f;
            Assert.That(killsPerMinute, Is.EqualTo(grenadierPerMinute * 0.5f).Within(grenadierPerMinute * 0.1f));
            Assert.AreEqual(TicksFor(minutes * 60f) / TicksFor(settings.buildings.sentryInterval), events.Count, 1, "Залп раз в sentryInterval");
            Assert.IsTrue(events.TrueForAll(volley => volley.BuildingId == headquarters.Id && volley.Shooters == settings.buildings.sentryShots));
        }

        [Test]
        public void Sentries_AimBeforeVolley()
        {
            var settings = TestSettings.Create();
            var match = new Match(3, settings);
            Building headquarters = match.AddHeadquarters(Side.Austria, AustriaHq);
            Squad target = match.AddSquad(Side.France, AustriaHq + new float2(-6f, 0f), 0f, Formation.Line);
            var events = new List<SentryVolley>();

            SentrySystem.Update(match, SimulationTime.TickDuration, events);
            Assert.IsTrue(headquarters.IsSentryAiming, "Сначала часовые целятся");
            Assert.AreEqual(target.Id, headquarters.SentryAimTargetId);
            Assert.AreEqual(0, events.Count, "Залп не сразу");

            int ticks = 0;
            while (events.Count == 0 && ticks < 100)
            {
                SentrySystem.Update(match, SimulationTime.TickDuration, events);
                ticks++;
            }

            Assert.AreEqual(TicksFor(settings.combat.aimTime), ticks, 1, "Залп через aimTime");
            Assert.IsFalse(headquarters.IsSentryAiming);
        }

        [Test]
        public void Sentries_AimCancelled_WhenTargetLeavesRange()
        {
            var settings = TestSettings.Create();
            var match = new Match(3, settings);
            Building headquarters = match.AddHeadquarters(Side.Austria, AustriaHq);
            Squad target = match.AddSquad(Side.France, AustriaHq + new float2(-6f, 0f), 0f, Formation.Line);
            var events = new List<SentryVolley>();

            SentrySystem.Update(match, SimulationTime.TickDuration, events);
            target.Position = AustriaHq + new float2(-40f, 0f);
            for (int i = 0; i < TicksFor(2f); i++)
            {
                SentrySystem.Update(match, SimulationTime.TickDuration, events);
            }

            Assert.AreEqual(0, events.Count, "Стрелять не по кому — залпа нет");
            Assert.IsFalse(headquarters.IsSentryAiming, "Часовые снова наготове");
        }

        [Test]
        public void Sentries_IgnoreEnemiesOutOfRange_AndBuildingsUnderConstruction()
        {
            var match = new Match(1, TestSettings.Create());
            match.AddHeadquarters(Side.Austria, AustriaHq);
            match.AddBarracks(Side.Austria, new float2(-30f, 0f));
            Squad far = match.AddSquad(Side.France, AustriaHq + new float2(-15f, 0f), 0f, Formation.Line);
            Squad nearUnfinished = match.AddSquad(Side.France, new float2(-34f, 0f), 0f, Formation.Line);
            int farAlive = far.Alive;
            int nearAlive = nearUnfinished.Alive;

            RunTicks(match, TicksFor(20f));

            Assert.AreEqual(farAlive, far.Alive, "Вне sentryRange часовые не стреляют");
            Assert.AreEqual(nearAlive, nearUnfinished.Alive, "Из строящейся казармы не стреляют");
        }

        [Test]
        public void Assault_DamagesBuilding_OnlyAfterReachingIt()
        {
            var match = new Match(1, TestSettings.Create());
            Building headquarters = match.AddHeadquarters(Side.Austria, AustriaHq);
            Squad squad = match.AddSquad(Side.France, AustriaHq + new float2(-20f, 0f), 0f, Formation.Column);
            match.Enqueue(new AssaultCommand(squad.Id, headquarters.Id));

            RunTicks(match, TicksFor(2f));
            Assert.AreEqual(SquadOrderType.Assault, squad.Order.Type);
            Assert.AreEqual(headquarters.MaxHealth, headquarters.Health, "Пока не добежал, здание целое");

            RunTicks(match, TicksFor(15f));
            Assert.LessOrEqual(math.distance(squad.Position, headquarters.Position), match.Settings.buildings.assaultRange + 1e-3f);
            Assert.Less(headquarters.Health, headquarters.MaxHealth, "Добежав, отряд бьёт здание в штыки");
        }

        [Test]
        public void Assault_DamageRate_IsSoldiersTimesAssaultDamage()
        {
            var match = new Match(1, TestSettings.Create());
            Building barracks = match.AddBarracks(Side.Austria, AustriaHq);
            Squad squad = match.AddSquad(Side.France, AustriaHq + new float2(-3f, 0f), 0f, Formation.Line, UnitType.Grenadiers, 20);
            match.Enqueue(new AssaultCommand(squad.Id, barracks.Id));

            RunTicks(match, TicksFor(10f));

            // Казарма строится — часовых нет, потерь нет: 20 × 0.5 × 10 с = 100.
            Assert.AreEqual(barracks.MaxHealth - 100f, barracks.Health, 0.5f);
        }

        [Test]
        public void DestroyedBarracks_IsRemoved_AndAssaultersStop()
        {
            var match = new Match(1, TestSettings.Create());
            Building barracks = match.AddBarracks(Side.Austria, AustriaHq);
            Squad squad = match.AddSquad(Side.France, AustriaHq + new float2(-3f, 0f), 0f, Formation.Line, UnitType.Grenadiers, 60);
            match.Enqueue(new AssaultCommand(squad.Id, barracks.Id));

            // 300 / (60 × 0.5) = 10 с.
            RunTicks(match, TicksFor(11f));

            Assert.IsNull(match.FindBuilding(barracks.Id), "Разрушенная казарма убрана с карты");
            Assert.IsTrue(barracks.IsDestroyed);
            Assert.AreEqual(SquadOrderType.None, squad.Order.Type);
        }

        [Test]
        public void AssaultCommand_IsIgnored_ForOwnBuilding()
        {
            var match = new Match(1, TestSettings.Create());
            Building own = match.AddHeadquarters(Side.France, AustriaHq);
            Squad squad = match.AddSquad(Side.France, AustriaHq + new float2(-10f, 0f), 0f, Formation.Line);
            match.Enqueue(new AssaultCommand(squad.Id, own.Id));

            match.Tick();

            Assert.AreEqual(SquadOrderType.None, squad.Order.Type);
        }

        [Test]
        public void AssaultCommand_IsIgnored_WhileChangingFormation()
        {
            // Как и «В штыки» на отряд, штурм во время перестроения не принимается.
            var match = new Match(1, TestSettings.Create());
            Building headquarters = match.AddHeadquarters(Side.Austria, AustriaHq);
            Squad squad = match.AddSquad(Side.France, AustriaHq + new float2(-10f, 0f), 0f, Formation.Line);
            match.Enqueue(new ChangeFormationCommand(squad.Id, Formation.Column));
            match.Tick();
            match.Enqueue(new AssaultCommand(squad.Id, headquarters.Id));
            match.Tick();

            Assert.AreEqual(SquadOrderType.None, squad.Order.Type);
        }
    }
}
