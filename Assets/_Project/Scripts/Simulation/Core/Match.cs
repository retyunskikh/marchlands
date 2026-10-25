using System;
using System.Collections.Generic;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Core
{
    /// <summary>
    /// Корень состояния симуляции. Продвигается только вызовом <see cref="Tick"/>, меняется извне только
    /// через команды. Работает без сцены и без UnityEngine, поэтому бои можно прогонять в тестах.
    /// </summary>
    public sealed class Match
    {
        private readonly CommandQueue commands = new CommandQueue();
        private readonly List<ICommand> currentTickCommands = new List<ICommand>();
        private readonly List<Squad> squads = new List<Squad>();
        private readonly List<VolleyEvent> lastTickVolleys = new List<VolleyEvent>();
        private int nextSquadId = 1;

        public Match(ulong seed, SimulationSettings settings)
        {
            Seed = seed;
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Random = new GameRandom(seed);
        }

        public ulong Seed { get; }

        public SimulationSettings Settings { get; }

        public GameRandom Random { get; }

        /// <summary>Номер тика, который будет выполнен следующим (он же — число уже выполненных тиков).</summary>
        public int CurrentTick { get; private set; }

        public int PendingCommandCount => commands.Count;

        public IReadOnlyList<Squad> Squads => squads;

        /// <summary>Залпы, произошедшие в последнем выполненном тике.</summary>
        public IReadOnlyList<VolleyEvent> LastTickVolleys => lastTickVolleys;

        /// <summary>Добавляет полный отряд. Используется при подготовке матча, до первого тика или из команд.</summary>
        public Squad AddSquad(Side side, float2 position, float facing, Formation formation)
        {
            return AddSquad(side, position, facing, formation, Settings.squads.squadSize);
        }

        public Squad AddSquad(Side side, float2 position, float facing, Formation formation, int alive)
        {
            if (alive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(alive));
            }

            var squad = new Squad(nextSquadId++, side, alive, position, facing, formation);
            squads.Add(squad);
            return squad;
        }

        public Squad FindSquad(int id)
        {
            foreach (Squad squad in squads)
            {
                if (squad.Id == id)
                {
                    return squad;
                }
            }

            return null;
        }

        /// <summary>Ставит команду на тик <paramref name="tick"/>. Прошедшие тики уже не изменить.</summary>
        public void Enqueue(int tick, ICommand command)
        {
            if (tick < CurrentTick)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), $"Тик {tick} уже выполнен, текущий тик — {CurrentTick}.");
            }

            commands.Enqueue(tick, command);
        }

        /// <summary>Ставит команду на ближайший невыполненный тик.</summary>
        public void Enqueue(ICommand command)
        {
            commands.Enqueue(CurrentTick, command);
        }

        public void Tick()
        {
            foreach (Squad squad in squads)
            {
                squad.PreviousPosition = squad.Position;
                squad.PreviousFacing = squad.Facing;
            }

            currentTickCommands.Clear();
            commands.TakeForTick(CurrentTick, currentTickCommands);
            foreach (ICommand command in currentTickCommands)
            {
                command.Execute(this);
            }

            lastTickVolleys.Clear();
            FireSystem.Update(this, SimulationTime.TickDuration, lastTickVolleys);

            foreach (Squad squad in squads)
            {
                SquadMovementSystem.Update(squad, Settings.squads, Settings.combat, SimulationTime.TickDuration);
            }

            SquadSeparationSystem.Resolve(squads, Settings.squads);

            // Отряд без солдат исчезает; распад малых отрядов и бегство — этап 3.
            squads.RemoveAll(squad => squad.Alive <= 0);

            CurrentTick++;
        }
    }
}
