using System;
using System.Collections.Generic;

namespace Marchlands.Simulation.Core
{
    /// <summary>
    /// Очередь команд, привязанных к номеру тика. Команды одного тика выполняются в порядке постановки —
    /// это часть детерминизма.
    /// </summary>
    public sealed class CommandQueue
    {
        private readonly SortedDictionary<int, List<ICommand>> commandsByTick = new SortedDictionary<int, List<ICommand>>();

        public int Count { get; private set; }

        public void Enqueue(int tick, ICommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (!commandsByTick.TryGetValue(tick, out List<ICommand> commands))
            {
                commands = new List<ICommand>();
                commandsByTick.Add(tick, commands);
            }

            commands.Add(command);
            Count++;
        }

        /// <summary>Переносит в <paramref name="result"/> все команды тика <paramref name="tick"/> и удаляет их из очереди.</summary>
        public void TakeForTick(int tick, List<ICommand> result)
        {
            if (!commandsByTick.TryGetValue(tick, out List<ICommand> commands))
            {
                return;
            }

            result.AddRange(commands);
            Count -= commands.Count;
            commandsByTick.Remove(tick);
        }
    }
}
