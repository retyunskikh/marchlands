using System;
using System.Collections.Generic;
using Marchlands.Simulation.AI;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Melee;
using Marchlands.Simulation.Morale;
using Marchlands.Simulation.Squads;
using Marchlands.Simulation.Terrain;
using Marchlands.Simulation.Victory;
using Marchlands.Simulation.Vision;
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
        private readonly List<SentryVolley> lastTickSentryVolleys = new List<SentryVolley>();
        private readonly List<ArtilleryShot> lastTickArtilleryShots = new List<ArtilleryShot>();
        private readonly List<RoundShotFlight> roundShots = new List<RoundShotFlight>();
        private readonly List<MeleeEngagement> engagements = new List<MeleeEngagement>();
        private readonly List<IMatchController> controllers = new List<IMatchController>();
        private readonly List<Village> villages = new List<Village>();
        private readonly PlayerState[] players = new PlayerState[SideCount];
        private int nextVillageId = 1;
        private readonly List<Building> buildings = new List<Building>();
        private readonly List<BlockhouseRuin> ruins = new List<BlockhouseRuin>();
        private readonly List<Village> lastTickCaptures = new List<Village>();
        private readonly List<Reinforcement> lastTickReinforcements = new List<Reinforcement>();
        private readonly List<Squad> lastTickFormedSquads = new List<Squad>();
        private readonly List<Disband> lastTickDisbands = new List<Disband>();
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

        /// <summary>Идёт ли дождь: порох сырой, часть ружей даёт осечку.</summary>
        public bool IsRaining { get; internal set; }

        /// <summary>Сколько секунд до смены погоды.</summary>
        internal float WeatherTimer { get; set; }

        internal bool WeatherStarted { get; set; }

        /// <summary>Залпы часовых зданий за последний выполненный тик.</summary>
        public IReadOnlyList<SentryVolley> LastTickSentryVolleys => lastTickSentryVolleys;

        /// <summary>Выстрелы пушек за последний выполненный тик.</summary>
        public IReadOnlyList<ArtilleryShot> LastTickArtilleryShots => lastTickArtilleryShots;

        /// <summary>Ядра в полёте и на рикошете.</summary>
        internal List<RoundShotFlight> RoundShots => roundShots;

        public IReadOnlyList<Village> Villages => villages;

        public IReadOnlyList<PlayerState> Players => players;

        public IReadOnlyList<Building> Buildings => buildings;

        /// <summary>Деревни, сменившие владельца в последнем выполненном тике.</summary>
        public IReadOnlyList<Village> LastTickCaptures => lastTickCaptures;

        /// <summary>Пополнения из деревень за последний тик — для отображения идущих к отряду людей.</summary>
        public IReadOnlyList<Reinforcement> LastTickReinforcements => lastTickReinforcements;

        /// <summary>Отряды, построенные из резерва за последний тик, — ввод выделяет новый отряд игрока.</summary>
        public IReadOnlyList<Squad> LastTickFormedSquads => lastTickFormedSquads;

        internal void ReportFormed(Squad squad)
        {
            lastTickFormedSquads.Add(squad);
        }

        /// <summary>Распущенные за последний тик люди резерва — отображение уводит их в казарму.</summary>
        public IReadOnlyList<Disband> LastTickDisbands => lastTickDisbands;

        internal void ReportDisband(Disband disband)
        {
            lastTickDisbands.Add(disband);
        }

        internal void ReportReinforcement(Squad squad, Village village)
        {
            lastTickReinforcements.Add(new Reinforcement(squad.Id, village.Id));
        }

        /// <summary>Действует ли туман войны: вражеские отряды видны только в обзоре своих.</summary>
        public bool FogOfWar { get; private set; }

        /// <summary>Включает туман войны (до начала матча, после расстановки).</summary>
        public void EnableFogOfWar()
        {
            FogOfWar = true;
            VisibilitySystem.Refresh(this);
        }

        /// <summary>Видит ли сторона <paramref name="viewer"/> отряд: свои — всегда, чужие — без тумана или в обзоре.</summary>
        public bool IsVisible(Squad squad, Side viewer)
        {
            return !FogOfWar || squad.Side == viewer || squad.SeenBy[(int)viewer];
        }

        /// <summary>Видит ли сторона здание: своё — всегда, чужое — без тумана или в обзоре (в том числе стройку).</summary>
        public bool IsVisible(Building building, Side viewer)
        {
            return !FogOfWar || building.Owner == viewer || VisibilitySystem.IsPointSeen(this, building.Position, viewer);
        }

        /// <summary>Радиус обзора в тумане войны, u.</summary>
        public float VisionRange => VisibilitySystem.Range(Settings);

        /// <summary>Местность полной карты; null — ровное поле без границ (песочницы).</summary>
        public TerrainMap Terrain { get; private set; }

        /// <summary>Поиск пути для ИИ по местности; null, если местности нет.</summary>
        public TerrainPathfinder Pathfinder { get; private set; }

        /// <summary>Задаёт местность до начала матча.</summary>
        public void SetTerrain(TerrainMap terrain)
        {
            Terrain = terrain;
            Pathfinder = terrain != null
                ? new TerrainPathfinder(terrain, Settings.terrain, TacticalAi.WaypointReach(Settings))
                : null;
        }

        /// <summary>Множитель скорости местности в точке; на ровном поле — 1.</summary>
        /// <summary>Множитель скорости в точке: местность и погода (в дождь все идут медленнее — грязь).</summary>
        public float TerrainSpeedMultiplier(float2 point)
        {
            float terrain = Terrain == null ? 1f : math.max(0f, Terrain.SpeedMultiplier(point, Settings.terrain));
            return IsRaining && Settings.weather.rainSpeedMultiplier > 0f ? terrain * Settings.weather.rainSpeedMultiplier : terrain;
        }

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

        /// <summary>Добавляет полный отряд гренадёров. Используется при подготовке матча, до первого тика или из команд.</summary>
        public Squad AddSquad(Side side, float2 position, float facing, Formation formation)
        {
            return AddSquad(side, position, facing, formation, UnitType.Grenadiers);
        }

        public Squad AddSquad(Side side, float2 position, float facing, Formation formation, int alive)
        {
            return AddSquad(side, position, facing, formation, UnitType.Grenadiers, alive);
        }

        /// <summary>Добавляет полный отряд выбранного рода войск.</summary>
        public Squad AddSquad(Side side, float2 position, float facing, Formation formation, UnitType type)
        {
            return AddSquad(side, position, facing, formation, type, Settings.Unit(type).squadSize);
        }

        public Squad AddSquad(Side side, float2 position, float facing, Formation formation, UnitType type, int alive)
        {
            if (alive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(alive));
            }

            var squad = new Squad(nextSquadId++, side, type, alive, position, facing, formation);
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

            var headquarters = new Building(nextBuildingId++, BuildingType.Headquarters, side, position, 0f, Settings.buildings.headquartersHealth);
            buildings.Add(headquarters);
            return headquarters;
        }

        /// <summary>Закладывает казарму. Проверки места и золота — в <see cref="BuildRules"/> и командах.</summary>
        internal Building AddBarracks(Side side, float2 position)
        {
            var barracks = new Building(nextBuildingId++, BuildingType.Barracks, side, position, Settings.buildings.barracksBuildTime, Settings.buildings.barracksHealth);
            buildings.Add(barracks);
            return barracks;
        }

        internal Building AddStable(Side side, float2 position)
        {
            var stable = new Building(nextBuildingId++, BuildingType.Stable, side, position, Settings.buildings.stableBuildTime, Settings.buildings.stableHealth);
            buildings.Add(stable);
            return stable;
        }

        internal Building AddDepot(Side side, float2 position)
        {
            var depot = new Building(nextBuildingId++, BuildingType.ArtilleryDepot, side, position, Settings.buildings.depotBuildTime, Settings.buildings.depotHealth);
            buildings.Add(depot);
            return depot;
        }

        /// <summary>Руины разрушенных блокгаузов, которые ещё не начали отстраивать.</summary>
        public IReadOnlyList<BlockhouseRuin> BlockhouseRuins => ruins;

        /// <summary>Закладывает блокгауз на месте руин: его строят рабочие из штаба (см. RepairSystem).</summary>
        internal Building RebuildBlockhouse(BlockhouseRuin ruin)
        {
            ruins.Remove(ruin);
            var blockhouse = new Building(nextBuildingId++, BuildingType.Blockhouse, ruin.Owner, ruin.Position,
                Settings.buildings.blockhouseRebuildTime, Settings.buildings.blockhouseHealth);
            buildings.Add(blockhouse);
            return blockhouse;
        }

        /// <summary>Ставит блокгауз стороны (есть с начала матча и уже построен).</summary>
        public Building AddBlockhouse(Side side, float2 position)
        {
            var blockhouse = new Building(nextBuildingId++, BuildingType.Blockhouse, side, position, 0f, Settings.buildings.blockhouseHealth);
            buildings.Add(blockhouse);
            return blockhouse;
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
            return TryGetRetreatPoint(side, from, BuildingType.Barracks, out point);
        }

        /// <summary>Куда бежит разбитый отряд: пехота — к казарме, конница — к конюшне, расчёт брошенной пушки — к штабу.</summary>
        public bool TryGetRetreatPoint(Squad squad, out float2 point)
        {
            BuildingType home = squad.IsArtillery ? BuildingType.Headquarters : BuildRules.GetHomeType(squad.Type);
            return TryGetRetreatPoint(squad.Side, squad.Position, home, out point);
        }

        private bool TryGetRetreatPoint(Side side, float2 from, BuildingType home, out float2 point)
        {
            Building nearestBarracks = null;
            float bestDistance = float.MaxValue;
            foreach (Building building in buildings)
            {
                if (building.Owner != side || building.Type != home || !building.IsBuilt)
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

        /// <summary>
        /// Куда отправлять приказы игрока в сетевой игре: если задан, <see cref="Enqueue(ICommand)"/> не ставит команду
        /// сам, а передаёт сюда (lockstep рассылает её и ставит на общий тик через <see cref="Enqueue(int, ICommand)"/>).
        /// </summary>
        public Action<ICommand> CommandRouter { get; set; }

        /// <summary>Ставит команду на ближайший невыполненный тик (в сетевой игре — передаёт в <see cref="CommandRouter"/>).</summary>
        public void Enqueue(ICommand command)
        {
            if (CommandRouter != null)
            {
                CommandRouter(command);
                return;
            }

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
            lastTickReinforcements.Clear();
            lastTickFormedSquads.Clear();
            lastTickDisbands.Clear();
            lastTickSentryVolleys.Clear();
            lastTickArtilleryShots.Clear();

            foreach (Squad squad in squads)
            {
                squad.PreviousPosition = squad.Position;
                squad.PreviousFacing = squad.Facing;
            }

            RecruitCampSystem.Update(this);
            WeatherSystem.Update(this, deltaTime);

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
            SentrySystem.Update(this, deltaTime, lastTickSentryVolleys);
            ArtillerySystem.Update(this, deltaTime, lastTickArtilleryShots);

            foreach (Squad squad in squads)
            {
                SquadMovementSystem.Update(squad, this, deltaTime);
                squad.IsOnHill = Terrain != null && Terrain.IsOnHill(squad.Position);
                squad.LastStep = squad.Position - squad.PreviousPosition;
            }

            MeleeSystem.Update(this, deltaTime);
            AssaultSystem.Update(this, deltaTime);
            RepairSystem.Update(this, deltaTime);
            MoraleSystem.Update(this, deltaTime);
            MeleeSystem.RemoveFinished(this);
            CaptureSystem.Update(this, deltaTime);
            ReinforcementSystem.Update(this, deltaTime);
            IncomeSystem.Update(this);
            BuildingSystem.Update(this, deltaTime);
            VictorySystem.Update(this, deltaTime);

            squads.RemoveAll(squad => squad.Alive <= 0 || squad.IsWithdrawn);
            if (FogOfWar)
            {
                VisibilitySystem.Update(this, deltaTime);
            }

            CurrentTick++;
        }

        /// <summary>
        /// Здание разрушено: казарма и конюшня убираются с карты (толпа ждущих у них расходится, резерв остаётся),
        /// разрушенный штаб остаётся — по нему засчитывается поражение.
        /// </summary>
        internal void DestroyBuilding(Building building)
        {
            building.Health = 0f;
            if (building.Type != BuildingType.Headquarters)
            {
                buildings.Remove(building);
            }

            if (building.Type == BuildingType.Blockhouse)
            {
                ruins.Add(new BlockhouseRuin(building.Owner, building.Position));
            }
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
