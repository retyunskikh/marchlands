using System;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    public class SquadMovementTests
    {
        private const int MaxTicks = 10000;

        private static Match CreateMatch()
        {
            return new Match(1, TestSettings.Create());
        }

        /// <summary>Выполняет тики, пока не выполнится условие; возвращает число выполненных тиков.</summary>
        private static int RunUntil(Match match, Func<bool> condition)
        {
            int ticks = 0;
            while (!condition())
            {
                Assert.Less(ticks, MaxTicks, "Условие не выполнилось за разумное время");
                match.Tick();
                ticks++;
            }

            return ticks;
        }

        private static int ExpectedTicks(float seconds)
        {
            return (int)math.ceil(seconds * SimulationTime.TicksPerSecond - 1e-3f);
        }

        // Время: разворот к точке (угол / 45°/s) + марш (дистанция / скорость) + разворот в конечный фронт.
        [TestCase(Formation.Line, 90f, 0f, 10f, 90f, 10f, TestName = "Line_StraightAhead_10s")]
        [TestCase(Formation.Line, 90f, 10f, 0f, 0f, 12f, TestName = "Line_TurnRight90_ThenMarch_12s")]
        [TestCase(Formation.Column, 0f, 16f, 0f, 180f, 14f, TestName = "Column_March_ThenAboutFace_14s")]
        [TestCase(Formation.Line, 0f, -10f, 0f, 0f, 18f, TestName = "Line_TargetFarBehind_TurnMarchTurn_18s")]
        public void Squad_ArrivesInExpectedTime(Formation formation, float facing, float targetX, float targetY, float finalFacing, float expectedSeconds)
        {
            Match match = CreateMatch();
            Squad squad = match.AddSquad(Side.France, float2.zero, facing, formation);
            var target = new float2(targetX, targetY);
            match.Enqueue(new MoveCommand(squad.Id, target, finalFacing));
            match.Tick();

            int ticks = 1 + RunUntil(match, () => squad.Order.Type == SquadOrderType.None);

            Assert.That(ticks, Is.InRange(ExpectedTicks(expectedSeconds) - 1, ExpectedTicks(expectedSeconds) + 1));
            Assert.AreEqual(0f, math.distance(target, squad.Position), 1e-4f);
        }

        [Test]
        public void ShortStepBack_WalksBackwards_WithoutTurning()
        {
            Match match = CreateMatch();
            Squad squad = match.AddSquad(Side.France, float2.zero, 0f, Formation.Line);
            var target = new float2(-4f, 0f);
            match.Enqueue(new MoveCommand(squad.Id, target, 180f));
            match.Tick();

            int ticks = 1 + RunUntil(match, () =>
            {
                Assert.AreEqual(0f, Angles.Delta(0f, squad.Facing), 1e-3f, "Пятится, не разворачиваясь");
                return squad.Order.Type == SquadOrderType.None;
            });

            // Вдвое медленнее обычного шага (1 u/s): 4 u за 8 с.
            Assert.That(ticks, Is.InRange(ExpectedTicks(8f) - 1, ExpectedTicks(8f) + 1));
            Assert.AreEqual(0f, math.distance(target, squad.Position), 1e-4f);
            Assert.AreEqual(0f, squad.Facing, 1e-3f, "Встаёт прежним фронтом");
        }

        [Test]
        public void ShortStepSideways_StillTurns()
        {
            Match match = CreateMatch();
            Squad squad = match.AddSquad(Side.France, float2.zero, 0f, Formation.Line);
            Assert.IsFalse(MoveCommand.IsBackstep(squad, new float2(0f, 4f), match.Settings.squads));
            Assert.IsFalse(MoveCommand.IsBackstep(squad, new float2(-10f, 0f), match.Settings.squads));
            Assert.IsTrue(MoveCommand.IsBackstep(squad, new float2(-4f, 1f), match.Settings.squads));
        }

        [Test]
        public void FinalFacing_MatchesOrder([Values(0f, 45f, -135f, 200f, 359f)] float finalFacing)
        {
            Match match = CreateMatch();
            Squad squad = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            match.Enqueue(new MoveCommand(squad.Id, new float2(5f, 5f), finalFacing));
            match.Tick();

            RunUntil(match, () => squad.Order.Type == SquadOrderType.None);

            Assert.AreEqual(0f, Angles.Delta(squad.Facing, finalFacing), 1e-3f);
        }

        [Test]
        public void Squad_TurnsToTargetBeforeMarching()
        {
            Match match = CreateMatch();
            Squad squad = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            match.Enqueue(new MoveCommand(squad.Id, new float2(10f, 0f), 0f));

            // Поворот на 90° при 45°/s занимает 2 с = 20 тиков; всё это время отряд стоит на месте.
            for (int i = 0; i < 19; i++)
            {
                match.Tick();
                Assert.AreEqual(0f, math.length(squad.Position), 1e-5f, $"Тик {i}");
            }

            match.Tick();
            match.Tick();
            Assert.Greater(squad.Position.x, 0f);
        }

        [Test]
        public void MoveCommand_ReplacesPreviousOrder()
        {
            Match match = CreateMatch();
            Squad squad = match.AddSquad(Side.France, float2.zero, 0f, Formation.Line);
            match.Enqueue(new MoveCommand(squad.Id, new float2(20f, 0f), 0f));
            match.Tick();
            match.Enqueue(new MoveCommand(squad.Id, new float2(1f, 0f), 0f));

            RunUntil(match, () => squad.Order.Type == SquadOrderType.None);

            Assert.AreEqual(1f, squad.Position.x, 1e-4f);
        }

        [Test]
        public void FormationChange_TakesConfiguredTime()
        {
            Match match = CreateMatch();
            Squad squad = match.AddSquad(Side.France, float2.zero, 0f, Formation.Line);
            match.Enqueue(new ChangeFormationCommand(squad.Id, Formation.Column));
            match.Tick();

            Assert.IsTrue(squad.IsChangingFormation);
            Assert.AreEqual(Formation.Line, squad.Formation);

            int ticks = 1 + RunUntil(match, () => squad.Formation == Formation.Column);

            Assert.That(ticks, Is.InRange(ExpectedTicks(4f) - 1, ExpectedTicks(4f) + 1));
            Assert.IsFalse(squad.IsChangingFormation);
        }

        [Test]
        public void FormationChange_CanBeReversedMidway_TakingTheTimeAlreadySpent()
        {
            Match match = CreateMatch();
            Squad squad = match.AddSquad(Side.France, float2.zero, 0f, Formation.Line);
            match.Enqueue(new ChangeFormationCommand(squad.Id, Formation.Column));
            match.Tick();
            for (int i = 0; i < 10; i++)
            {
                match.Tick();
            }

            // Через 1 с из 4 — обратно в линию: возврат длится столько же, сколько уже прошло.
            match.Enqueue(new ChangeFormationCommand(squad.Id, Formation.Line));
            match.Tick();
            Assert.IsTrue(squad.IsChangingFormation);
            Assert.AreEqual(Formation.Line, squad.TargetFormation);
            Assert.That(squad.FormationChangeProgress, Is.EqualTo(0.75f).Within(0.03f), "Возврат начинается с той же точки");

            for (int i = 0; i < 10 && squad.IsChangingFormation; i++)
            {
                match.Tick();
            }

            Assert.IsFalse(squad.IsChangingFormation, "Возврат быстрее полного перестроения");
            Assert.AreEqual(Formation.Line, squad.Formation);
        }

        [Test]
        public void FormationChange_ToSameTarget_IsIgnored()
        {
            Match match = CreateMatch();
            Squad squad = match.AddSquad(Side.France, float2.zero, 0f, Formation.Line);
            match.Enqueue(new ChangeFormationCommand(squad.Id, Formation.Column));
            match.Tick();
            float remaining = squad.FormationChangeRemaining;
            match.Enqueue(new ChangeFormationCommand(squad.Id, Formation.Column));
            match.Tick();

            Assert.Less(squad.FormationChangeRemaining, remaining, "Таймер не сбрасывается");
        }

        [Test]
        public void SquadMarchesSlowlyWhileFormingColumn_ThenAtColumnSpeed()
        {
            Match match = CreateMatch();
            Squad squad = match.AddSquad(Side.France, float2.zero, 0f, Formation.Line);
            var target = new float2(8f, 0f);
            match.Enqueue(new ChangeFormationCommand(squad.Id, Formation.Column));
            match.Enqueue(new MoveCommand(squad.Id, target, 0f));

            int changeTicks = RunUntil(match, () => !squad.IsChangingFormation && match.CurrentTick > 0);
            // Перестроение 4 с со скоростью линии × 0.4: 4 × 1.0 × 0.4 = 1.6 u.
            Assert.AreEqual(1.6f, squad.Position.x, 0.15f, "Во время перестроения отряд уже идёт, но медленно");

            // Остаток колонной: 6.4 u / 1.6 u/s = 4 с.
            int marchTicks = RunUntil(match, () => squad.Order.Type == SquadOrderType.None);

            Assert.That(changeTicks, Is.InRange(ExpectedTicks(4f) - 1, ExpectedTicks(4f) + 1));
            Assert.That(marchTicks, Is.InRange(ExpectedTicks(4f) - 1, ExpectedTicks(4f) + 1));
            Assert.AreEqual(0f, math.distance(target, squad.Position), 1e-4f);
        }

        [Test]
        public void SquadMarchesSlowlyWhileFormingLine_ThenContinuesMoveOrder()
        {
            Match match = CreateMatch();
            Squad squad = match.AddSquad(Side.France, float2.zero, 0f, Formation.Column);
            var target = new float2(8f, 0f);
            match.Enqueue(new ChangeFormationCommand(squad.Id, Formation.Line));
            match.Enqueue(new MoveCommand(squad.Id, target, 0f));

            RunUntil(match, () => !squad.IsChangingFormation && match.CurrentTick > 0);
            // Перестроение 4 с со скоростью колонны × 0.4: 4 × 1.6 × 0.4 = 2.56 u.
            Assert.AreEqual(2.56f, squad.Position.x, 0.2f, "Перестраиваясь в линию, отряд уже идёт, но медленно");

            RunUntil(match, () => squad.Order.Type == SquadOrderType.None);
            Assert.AreEqual(0f, math.distance(target, squad.Position), 1e-4f);
        }

        [Test]
        public void TurnOrder_IsDoneDuringFormationChange()
        {
            var match = new Match(1, TestSettings.Create());
            Squad squad = match.AddSquad(Side.France, float2.zero, 90f, Formation.Line);
            match.Enqueue(new ChangeFormationCommand(squad.Id, Formation.Column));
            match.Enqueue(new MoveCommand(squad.Id, float2.zero, 0f));

            match.Tick();
            match.Tick();

            Assert.IsTrue(squad.IsChangingFormation, "Перестроение ещё идёт");
            Assert.Less(math.abs(Angles.Delta(squad.Facing, 0f)), 90f, "А разворот уже начался");
        }
    }
}
