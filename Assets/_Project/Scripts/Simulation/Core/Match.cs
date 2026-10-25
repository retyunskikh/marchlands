using System;
using System.Collections.Generic;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Melee;
using Marchlands.Simulation.Morale;
using Marchlands.Simulation.Squads;
using Marchlands.Simulation.Victory;
using Unity.Mathematics;

namespace Marchlands.Simulation.Core
{
    /// <summary>
    /// Корень состояния симуляции. Продвигается только вызовом <see cref="Tick"/>, меняется извне только
    /// через команды. Работает без сцены и без UnityEngine, поэтому бои можно прогонять в тестах.
    /// </summary>
    public sealed class Match
    {
        private const int SideCount = 2;

        private readonly CommandQueue commands = new CommandQueue();
        private readonly List<ICommand> currentTickCommands = new List<ICommand>();
        private readonly List<Squad> squads = new List<Squad>();
        private readonly List<VolleyEvent> lastTickVolleys = new List<VolleyEvent>();
        private readonly List<MeleeEngagement> engagements = new List<MeleeEngagement>();
        private readonly List<IMatchController> controllers = new List<IMatchController>();
        private readonly List<Village> villages = new List<Village>();
        private readonly PlayerState[] players = new PlayerState[SideCount];
        private int nextVillageId = 1;
        private readonly List<Building> buildings = new List<Building>();
        private readonly List<Village> lastTickCaptures = new List<Village>();
        private readonly float[] dominationTimes = new float[SideCount];
        private int nextBuildingId = 1;
        private readonly float2[] retreatPoints = new float2[SideCount];
        private readonly bool[] hasRetreatPoint = new bool[SideCount];
        private int nextSquadId = 1;

        public Match(ulong seed, SimulationSettings settings)
        {
            Seed = seed;
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Random = new GameRandom(seed);
            players[(int)Side.France] = new PlayerState(Side.France, settings.economy.startGold);
            players[(int)Side.Austria] = new PlayerState(Side.Austria, settings.economy.startGold);
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

        public IReadOnlyList<Village> Villages => villages;

        public IReadOnlyList<PlayerState> Players => players;

        public IReadOnlyList<Building> Buildings => buildings;

        /// <summary>Деревни, сменившие владельца в последнем выполненном тике.</summary>
        public IReadOnlyList<Village> LastTickCaptures => lastTickCaptures;

        /// <summary>Проверяются ли условия победы. В песочницах выключены.</summary>
        public bool VictoryEnabled { get; private set; }

        /// <summary>Итог матча; null — матч идёт. После итога тики больше ничего не меняют.</summary>
        public MatchResult Result { get; private set; }

        public void EnableVictoryConditions()
        {
            VictoryEnabled = true;
        }

        /// <summary>Сколько секунд сторона непрерывно держит долю деревень для доминирования.</summary>
        public float GetDominationTime(Side side)
        {
            return dominationTimes[(int)side];
        }

        /// <summary>Сколько деревень нужно держать для доминирования (0 — деревень нет).</summary>
        public int DominationVillagesRequired => VictorySystem.RequiredVillages(this);

        internal void SetDominationTime(Side side, float seconds)
        {
            dominationTimes[(int)side] = seconds;
        }

        internal void Finish(Side winner, VictoryReason reason)
        {
            Result ??= new MatchResult(winner, reason, CurrentTick);
        }

        internal void RecordCapture(Village village)
        {
            lastTickCaptures.Add(village);
        }

        /// <summary>Идущие рукопашные.</summary>
        public IReadOnlyList<MeleeEngagement> Engagements => engagements;

        /// <summary>Отряды, побежавшие в этом тике; их бегство ещё не ударило по морали союзников.</summary>
        internal Queue<Squad> PendingRouts { get; } = new Queue<Squad>();

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

        public Village AddVillage(float2 position)
        {
            var village = new Village(nextVillageId++, position);
            villages.Add(village);
            return village;
        }

        /// <summary>Ставит штаб стороны (он есть с начала матча и уже построен).</summary>
        public Building AddHeadquarters(Side side, float2 position)
        {
            if (GetHeadquarters(side) != null)
            {
                throw new InvalidOperationException($"У стороны {side} уже есть штаб.");
            }

            var headquarters = new Building(nextBuildingId++, BuildingType.Headquarters, side, position, 0f);
            buildings.Add(headquarters);
            return headquarters;
        }

        /// <summary>Закладывает казарму. Проверки места и золота — в <see cref="BuildRules"/> и командах.</summary>
        internal Building AddBarracks(Side side, float2 position)
        {
            var barracks = new Building(nextBuildingId++, BuildingType.Barracks, side, position, Settings.buildings.barracksBuildTime);
            buildings.Add(barracks);
            return barracks;
        }

        public Building GetHeadquarters(Side side)
        {
            foreach (Building building in buildings)
            {
                if (building.Owner == side && building.Type == BuildingType.Headquarters)
                {
                    return building;
                }
            }

            return null;
        }

        public Building FindBuilding(int id)
        {
            foreach (Building building in buildings)
            {
                if (building.Id == id)
                {
                    return building;
                }
            }

            return null;
        }

        public PlayerState GetPlayer(Side side)
        {
            return players[(int)side];
        }

        public int CountVillages(Side side)
        {
            int count = 0;
            foreach (Village village in villages)
            {
                if (village.Owner == side)
                {
                    count++;
                }
            }

            return count;
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

        /// <summary>Подключает ИИ: он будет вызываться в начале каждого тика в порядке подключения.</summary>
        public void AddController(IMatchController controller)
        {
            controllers.Add(controller ?? throw new ArgumentNullException(nameof(controller)));
        }

        /// <summary>Разгромлена ли сторона: все её отряды бегут или погибли. Сторона без отрядов тоже разгромлена.</summary>
        public bool IsSideBroken(Side side)
        {
            foreach (Squad squad in squads)
            {
                if (squad.Side == side && !squad.IsRouting)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Куда бегут разбитые отряды стороны, если у неё нет штаба (песочницы).</summary>
        public void SetRetreatPoint(Side side, float2 point)
        {
            retreatPoints[(int)side] = point;
            hasRetreatPoint[(int)side] = true;
        }

        /// <summary>
        /// Куда бегут разбитые отряды стороны: к ближайшей своей построенной казарме, без неё — к штабу,
        /// а в песочницах без зданий — к точке отхода из сценария.
        /// </summary>
        public bool TryGetRetreatPoint(Side side, float2 from, out float2 point)
        {
            Building nearestBarracks = null;
            float bestDistance = float.MaxValue;
            foreach (Building building in buildings)
            {
                if (building.Owner != side || building.Type != BuildingType.Barracks || !building.IsBuilt)
                {
                    continue;
                }

                float distance = math.distancesq(building.Position, from);
                if (distance < bestDistance)
                {
                    nearestBarracks = building;
                    bestDistance = distance;
                }
            }

            if (nearestBarracks != null)
            {
                point = nearestBarracks.Position;
                return true;
            }

            Building headquarters = GetHeadquarters(side);
            if (headquarters != null)
            {
                point = headquarters.Position;
                return true;
            }

            point = retreatPoints[(int)side];
            return hasRetreatPoint[(int)side];
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
            if (Result != null)
            {
                return;
            }

            float deltaTime = SimulationTime.TickDuration;
            lastTickCaptures.Clear();

            foreach (Squad squad in squads)
            {
                squad.PreviousPosition = squad.Position;
                squad.PreviousFacing = squad.Facing;
            }

            foreach (IMatchController controller in controllers)
            {
                controller.Update(this);
            }

            currentTickCommands.Clear();
            commands.TakeForTick(CurrentTick, currentTickCommands);
            foreach (ICommand command in currentTickCommands)
            {
                command.Execute(this);
            }

            lastTickVolleys.Clear();
            FireSystem.Update(this, deltaTime, lastTickVolleys);

            foreach (Squad squad in squads)
            {
                SquadMovementSystem.Update(squad, this, deltaTime);
            }

            MeleeSystem.Update(this, deltaTime);
            MoraleSystem.Update(this, deltaTime);
            MeleeSystem.RemoveFinished(this);
            CaptureSystem.Update(this, deltaTime);
            IncomeSystem.Update(this);
            BuildingSystem.Update(this, deltaTime);
            VictorySystem.Update(this, deltaTime);

            squads.RemoveAll(squad => squad.Alive <= 0 || squad.IsWithdrawn);

            CurrentTick++;
        }

        internal void AddEngagement(MeleeEngagement engagement)
        {
            engagements.Add(engagement);
        }

        internal void RemoveEngagementAt(int index)
        {
            engagements.RemoveAt(index);
        }
    }
}
