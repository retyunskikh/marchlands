using System;
using System.Collections.Generic;
using Marchlands.Simulation.Core;
using NUnit.Framework;

namespace Marchlands.Tests.EditMode
{
    public class MatchTests
    {
        private sealed class RecordingCommand : ICommand
        {
            private readonly string name;
            private readonly List<string> log;

            public RecordingCommand(string name, List<string> log)
            {
                this.name = name;
                this.log = log;
            }

            public void Execute(Match match)
            {
                log.Add($"{name}@{match.CurrentTick}");
            }
        }

        [Test]
        public void Tick_AdvancesCounter()
        {
            var match = new Match(1);

            match.Tick();
            match.Tick();

            Assert.AreEqual(2, match.CurrentTick);
        }

        [Test]
        public void Command_RunsExactlyOnItsTick()
        {
            var match = new Match(1);
            var log = new List<string>();
            match.Enqueue(3, new RecordingCommand("A", log));

            for (int i = 0; i < 6; i++)
            {
                match.Tick();
            }

            CollectionAssert.AreEqual(new[] { "A@3" }, log);
            Assert.AreEqual(0, match.PendingCommandCount);
        }

        [Test]
        public void CommandsOfOneTick_RunInEnqueueOrder()
        {
            var match = new Match(1);
            var log = new List<string>();
            match.Enqueue(1, new RecordingCommand("B", log));
            match.Enqueue(0, new RecordingCommand("A", log));
            match.Enqueue(1, new RecordingCommand("C", log));

            match.Tick();
            match.Tick();

            CollectionAssert.AreEqual(new[] { "A@0", "B@1", "C@1" }, log);
        }

        [Test]
        public void EnqueueWithoutTick_RunsOnNextTick()
        {
            var match = new Match(1);
            var log = new List<string>();
            match.Tick();

            match.Enqueue(new RecordingCommand("A", log));
            match.Tick();

            CollectionAssert.AreEqual(new[] { "A@1" }, log);
        }

        [Test]
        public void EnqueueForPastTick_Throws()
        {
            var match = new Match(1);
            match.Tick();
            match.Tick();

            Assert.Throws<ArgumentOutOfRangeException>(() => match.Enqueue(1, new RecordingCommand("A", new List<string>())));
        }

        [Test]
        public void SameSeed_GivesSameMatchRandom()
        {
            var first = new Match(77);
            var second = new Match(77);

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(first.Random.NextUInt(), second.Random.NextUInt());
            }
        }
    }
}
