using System.Collections.Generic;
using Marchlands.Bootstrap.Config;
using Marchlands.Input;
using Marchlands.Simulation.AI;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Commands;
using Marchlands.Network;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Marchlands.Simulation.Terrain;
using Marchlands.UI;
using Marchlands.View;
using Unity.Mathematics;
using UnityEngine;

namespace Marchlands.Bootstrap
{
    /// <summary>
    /// Связывает симуляцию со сценой: создаёт матч по конфигам, в каждом кадре выполняет нужное число
    /// фиксированных тиков и отрисовывает результат с интерполяцией.
    /// </summary>
    public sealed class BattleBootstrap : MonoBehaviour
    {
        [SerializeField] private BalanceConfig balance;
        [SerializeField] private ScenarioConfig scenario;
        [Tooltip("Карты на выбор в главном меню, по порядку карточек; пусто — всегда scenario (песочницы).")]
        [SerializeField] private ScenarioConfig[] maps;
        [Tooltip("Карта обучения (уроки ведёт tutorial).")]
        [SerializeField] private ScenarioConfig tutorialScenario;
        [SerializeField] private TutorialDirector tutorial;
        [SerializeField] private int seed = 1;

        [Header("Сетевая игра")]
        [Tooltip("Начальная задержка приказа в тиках (одинакова у обеих машин), дальше — по измеренному пингу.")]
        [SerializeField] private int networkInputDelay = 3;
        [Tooltip("Пределы задержки приказа, тиков: в локальной сети — 1 (0,1 с).")]
        [SerializeField] private int networkMinDelay = 1;
        [SerializeField] private int networkMaxDelay = 8;
        [Tooltip("Запас сверх пути приказа до соперника, мс (не меньше двух разбросов пинга).")]
        [SerializeField] private float networkLatencyMargin = 25f;
        [Tooltip("Как часто пересчитывать задержку по пингу, с.")]
        [SerializeField] private float networkAdaptInterval = 1f;
        [Tooltip("Раз в столько тиков стороны сверяют контрольную сумму состояния.")]
        [SerializeField] private int networkHashInterval = 10;
        [Tooltip("Сколько тиков можно догонять за раз после задержки соперника.")]
        [SerializeField] private int networkMaxBacklog = 10;
        [Tooltip("Через сколько секунд ожидания приказов соперника показывать «Ожидание соперника…».")]
        [SerializeField] private float networkWaitingNotice = 1f;
        [SerializeField] private int targetFrameRate = 60;

        [Header("Сцена")]
        [SerializeField] private TerrainView terrainView;
        [Tooltip("Фон поля: растягивается на размер карты сценария.")]
        [SerializeField] private Transform background;
        [SerializeField] private SquadViewRegistry squadViews;
        [SerializeField] private VillageViewRegistry villageViews;
        [SerializeField] private BuildingViewRegistry buildingViews;
        [SerializeField] private FireArcView fireArc;
        [SerializeField] private VolleyEffectsView volleyEffects;
        [SerializeField] private RainView rainView;
        [SerializeField] private BattleSoundsView battleSounds;
        [SerializeField] private MusicDirector music;
        [SerializeField] private BattleInputController input;
        [SerializeField] private OrderPanel orderPanel;
        [SerializeField] private SquadCard squadCard;
        [SerializeField] private CampPanel campPanel;
        [SerializeField] private TopBar topBar;
        [SerializeField] private BuildPanel buildPanel;
        [SerializeField] private BuildingPanel buildingPanel;
        [SerializeField] private FormSquadPanel formSquadPanel;
        [SerializeField] private GameMenu gameMenu;
        [Tooltip("Мышь и клавиатура на ПК (на телефоне не действует).")]
        [SerializeField] private DesktopControls desktopControls;

        private Match match;
        private FixedStepClock clock;

        private bool isTutorial;
        private Side playerSide;
        private MatchSetup network;
        private LockstepSession lockstep;
        private int backlog;
        private float waitingTime;
        private float adaptTimer;
        private float stallSeconds;
        private int stallExtra;
        private int quietSeconds;

        private sealed class PeerLink : ILockstepLink
        {
            public void Send(byte[] message)
            {
                NetworkGame.SendToPeer(message);
            }
        }
        private GatedController enemyTactics;
        private GatedController enemyStrategy;

        public Match Match => match;

        /// <summary>Доля шага между последним выполненным тиком и следующим, в [0, 1).</summary>
        public float InterpolationAlpha => clock.Alpha;

        private void Awake()
        {
            // На мобильных Unity по умолчанию ограничивает частоту 30 кадрами.
            Application.targetFrameRate = targetFrameRate;

            // Сетевой матч: карта, seed и сторона — из лобби, ИИ нет.
            network = NetworkGame.IsActive ? NetworkGame.Setup : null;
            isTutorial = network == null && MapChoice.Tutorial && tutorialScenario != null && tutorial != null;
            if (network != null && maps != null && maps.Length > 0)
            {
                scenario = maps[Mathf.Clamp(network.Map, 0, maps.Length - 1)];
            }
            else if (isTutorial)
            {
                scenario = tutorialScenario;
            }
            else if (maps != null && maps.Length > 0)
            {
                scenario = maps[Mathf.Clamp(MapChoice.Selected, 0, maps.Length - 1)];
            }

            playerSide = network != null ? network.LocalSide : scenario.PlayerSide;
            // Нации сторон: в сетевом бою — выбранные игроками (могут совпасть), иначе Франция и Австрия.
            if (network != null)
            {
                Armies.Configure(network.NationOf(Side.France), network.NationOf(Side.Austria));
            }
            else
            {
                Armies.Reset();
            }
            match = new Match(network != null ? network.Seed : unchecked((ulong)seed), balance.CreateSettings());
            clock = new FixedStepClock(SimulationTime.TicksPerSecond, SimulationTime.MaxTicksPerAdvance);
            SpawnScenario();

            if (scenario.HasTerrain)
            {
                // Размер карты — из сценария: фон, край леса и границы камеры (вверх-вниз — до опушки).
                Vector2 size = scenario.MapSize;
                terrainView.ApplySeason(scenario.Season);
                terrainView.Initialize(match, size);
                if (scenario.Season != null && Camera.main != null)
                {
                    Camera.main.backgroundColor = scenario.Season.OutsideColor;
                }

                if (background != null)
                {
                    SpriteRenderer backgroundRenderer = background.GetComponent<SpriteRenderer>();
                    if (scenario.Season != null && backgroundRenderer != null)
                    {
                        backgroundRenderer.color = scenario.Season.FieldColor;
                    }

                    Vector2 spriteSize = backgroundRenderer != null && backgroundRenderer.sprite != null ? (Vector2)backgroundRenderer.sprite.bounds.size : Vector2.one;
                    background.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);
                }

                float fieldHeight = scenario.TerrainMax.y - scenario.TerrainMin.y;
                input.SetMapBounds((scenario.TerrainMin + scenario.TerrainMax) * 0.5f, new Vector2(size.x, fieldHeight + 2f));
            }
            else
            {
                terrainView.Initialize(match);
            }
            // Туман войны — в обычном бою по выбору в меню; в обучении и песочницах его нет.
            if (network != null ? network.FogOfWar : !isTutorial && maps != null && maps.Length > 0 && MapChoice.FogOfWar)
            {
                match.EnableFogOfWar();
            }

            squadViews.Initialize(match, playerSide);
            if (match.FogOfWar && scenario.HasTerrain)
            {
                // Пелена тумана над картой и лесами за краем.
                var fog = new GameObject("Fog of War").AddComponent<FogOfWarView>();
                fog.Initialize(match, playerSide, (scenario.TerrainMin + scenario.TerrainMax) * 0.5f, scenario.MapSize);
            }

            villageViews.Initialize(match, scenario.VillageLayouts, scenario.GetVillageDoors);
            buildingViews.Initialize(match);
            fireArc.Initialize(match);
            volleyEffects.Initialize(match, buildingViews);
            if (rainView != null)
            {
                rainView.Initialize(match);
            }
            battleSounds.Initialize(match, playerSide);
            music.Initialize(match, playerSide);
            input.Initialize(match, playerSide);
            orderPanel.Initialize(match);
            if (squadCard != null)
            {
                squadCard.Initialize(match, playerSide);
            }
            topBar.Initialize(match, playerSide);
            buildPanel.Initialize(match, playerSide);
            buildingPanel.Initialize(match, playerSide);
            formSquadPanel.Initialize(match, playerSide);
            gameMenu.Initialize(match, playerSide, music);
            if (campPanel != null)
            {
                campPanel.Initialize(match, playerSide);
            }

            if (isTutorial && enemyTactics != null)
            {
                tutorial.Initialize(match, playerSide, input, enemyTactics, enemyStrategy);
            }

            if (network != null)
            {
                StartNetworkMatch();
            }

            input.SelectionChanged += OnSelectionChanged;
            input.GroupChanged += OnGroupChanged;
            input.BuildingSelectionChanged += OnBuildingSelectionChanged;
            input.CampSelectionChanged += OnCampSelectionChanged;
            input.ChargeTargetingChanged += orderPanel.SetChargeTargeting;
            input.PlacementChanged += buildPanel.SetPlacementState;
            orderPanel.ChargeRequested += input.BeginChargeTargeting;
            buildingPanel.BuildRequested += input.BeginPlacement;
            buildPanel.ConfirmRequested += input.ConfirmPlacement;
            buildPanel.CancelRequested += input.CancelPlacement;
            if (desktopControls != null)
            {
                desktopControls.VolleyPressed += orderPanel.Volley;
                desktopControls.ChargePressed += orderPanel.Charge;
                desktopControls.FormationPressed += orderPanel.ToggleFormation;
                desktopControls.StopPressed += orderPanel.Stop;
                desktopControls.PausePressed += gameMenu.TogglePause;
                desktopControls.HelpPressed += gameMenu.ToggleHelp;
            }
        }

        private void OnDestroy()
        {
            if (input == null)
            {
                return;
            }

            input.SelectionChanged -= OnSelectionChanged;
            input.GroupChanged -= OnGroupChanged;
            input.BuildingSelectionChanged -= OnBuildingSelectionChanged;
            input.CampSelectionChanged -= OnCampSelectionChanged;
            if (orderPanel != null)
            {
                input.ChargeTargetingChanged -= orderPanel.SetChargeTargeting;
                orderPanel.ChargeRequested -= input.BeginChargeTargeting;
            }

            if (buildPanel != null)
            {
                input.PlacementChanged -= buildPanel.SetPlacementState;
                buildingPanel.BuildRequested -= input.BeginPlacement;
                buildPanel.ConfirmRequested -= input.ConfirmPlacement;
                buildPanel.CancelRequested -= input.CancelPlacement;
            }
        }

        /// <summary>Сетевой матч: приказы игрока уходят в lockstep, сообщения соперника — в него же; камера — у своего штаба.</summary>
        private void StartNetworkMatch()
        {
            lockstep = new LockstepSession(match, playerSide, networkInputDelay, networkHashInterval, new PeerLink());
            match.CommandRouter = lockstep.Submit;
            lockstep.Desynced += () => gameMenu.ShowNetworkEnd(false, true);
            NetworkGame.Handler = OnNetworkMessage;
            NetworkGame.Disconnected += connection =>
            {
                if (connection == NetworkGame.PeerConnection || !NetworkGame.Session.IsHost)
                {
                    gameMenu.ShowNetworkEnd(false, false);
                }
            };
            gameMenu.SetNetworkMatch();

            Building headquarters = match.GetHeadquarters(playerSide);
            if (playerSide != scenario.PlayerSide && headquarters != null)
            {
                input.CameraRig.CenterOn(headquarters.Position);
            }
        }

        private void OnNetworkMessage(int connection, byte[] message)
        {
            if (LobbyProtocol.TypeOf(message) == NetMessageType.Quit)
            {
                gameMenu.ShowNetworkEnd(true, false);
                return;
            }

            lockstep?.Receive(message);
        }

        /// <summary>Тики, которые пора выполнить: в сетевой игре — пока есть приказы обеих сторон (догоняя отставание).</summary>
        private int RunTicks()
        {
            int ticks = clock.Advance(Time.deltaTime);
            if (lockstep == null)
            {
                for (int i = 0; i < ticks; i++)
                {
                    match.Tick();
                    AfterTick();
                }

                return ticks;
            }

            if (gameMenu.IsOver)
            {
                return 0;
            }

            backlog = Mathf.Min(backlog + ticks, networkMaxBacklog);
            int done = 0;
            while (backlog > 0 && lockstep.TryAdvance())
            {
                backlog--;
                done++;
                AfterTick();
            }

            bool waiting = lockstep.IsWaiting && backlog > 0;
            waitingTime = waiting ? waitingTime + Time.unscaledDeltaTime : 0f;
            stallSeconds += waiting ? Time.unscaledDeltaTime : 0f;
            gameMenu.SetWaiting(waitingTime >= networkWaitingNotice);
            adaptTimer += Time.unscaledDeltaTime;
            if (adaptTimer >= networkAdaptInterval)
            {
                adaptTimer = 0f;
                AdaptDelay();
            }

            return done;
        }

        /// <summary>
        /// Задержка приказа по сети: путь приказа до соперника (напрямую — половина пинга, через сервер — пинг до
        /// сервера: половина у нас плюс столько же у соперника) плюс запас, в тиках. Растёт сразу, уменьшается по тику
        /// в секунду. Если всё же ждём соперника — добавляется тик, пока 10 с не будет спокойно.
        /// </summary>
        private void AdaptDelay()
        {
            if (stallSeconds > 0.15f)
            {
                stallExtra = Mathf.Min(stallExtra + 1, 3);
                quietSeconds = 0;
            }
            else if (++quietSeconds >= 10 && stallExtra > 0)
            {
                stallExtra--;
                quietSeconds = 0;
            }

            stallSeconds = 0f;
            NetworkSession session = NetworkGame.Session;
            if (session == null || !session.TryGetLatency(NetworkGame.PeerConnection, out float roundTrip, out float jitter))
            {
                return;
            }

            float oneWay = NetworkGame.IsRelay ? roundTrip : roundTrip * 0.5f;
            float needed = oneWay + Mathf.Max(networkLatencyMargin, 2f * jitter);
            float tickMs = 1000f / SimulationTime.TicksPerSecond;
            int target = Mathf.Clamp(Mathf.CeilToInt(needed / tickMs) + stallExtra, networkMinDelay, networkMaxDelay);
            int current = lockstep.InputDelay;
            lockstep.InputDelay = target > current ? target : target < current ? current - 1 : current;
            gameMenu.SetNetworkInfo(Mathf.RoundToInt(roundTrip), lockstep.InputDelay * tickMs / 1000f);
        }

        private void Update()
        {
            RunTicks();

            squadViews.Render(clock.Alpha);
            villageViews.Render();
            buildingViews.Render();
            fireArc.Render(clock.Alpha);
        }

        private void AfterTick()
        {
            volleyEffects.Play(match.LastTickVolleys);
            volleyEffects.PlaySentries(match.LastTickSentryVolleys);
            volleyEffects.PlayArtillery(match.LastTickArtilleryShots);
            buildingViews.PlaySentries(match.LastTickSentryVolleys);
            battleSounds.Play(match.LastTickCaptures);
            villageViews.Play(match.LastTickReinforcements);
            buildingViews.PlayDisbands(match.LastTickDisbands);
            SelectFormedSquad();
        }

        /// <summary>Только что построенный отряд игрока сразу выделяется.</summary>
        private void SelectFormedSquad()
        {
            foreach (Squad formed in match.LastTickFormedSquads)
            {
                if (formed.Side == playerSide)
                {
                    input.SelectSquad(formed);
                }
            }
        }

        private void SpawnScenario()
        {
            if (scenario.HasTerrain)
            {
                match.SetTerrain(BuildTerrain());
            }

            if (scenario.VictoryConditions)
            {
                match.EnableVictoryConditions();
            }

            match.SetRetreatPoint(Side.France, scenario.GetRetreatPoint(Side.France));
            match.SetRetreatPoint(Side.Austria, scenario.GetRetreatPoint(Side.Austria));

            foreach (Side side in new[] { Side.France, Side.Austria })
            {
                if (scenario.HasHeadquarters)
                {
                    match.AddHeadquarters(side, scenario.GetHeadquarters(side));
                }

                foreach (Vector2 blockhouse in scenario.GetBlockhouses(side))
                {
                    match.AddBlockhouse(side, blockhouse);
                }

                if (scenario.IsAiControlled(side) && isTutorial)
                {
                    // В обучении противник включается по ходу уроков.
                    enemyTactics = new GatedController(new TacticalAi(side));
                    enemyStrategy = new GatedController(new StrategicAi(side));
                    match.AddController(enemyTactics);
                    match.AddController(enemyStrategy);
                }
                else if (scenario.IsAiControlled(side) && network == null)
                {
                    match.AddController(new TacticalAi(side));
                    match.AddController(new StrategicAi(side));
                }
            }

            foreach (Vector2 village in scenario.Villages)
            {
                match.AddVillage(village);
            }

            foreach (ScenarioConfig.SquadSpawn spawn in scenario.Squads)
            {
                match.AddSquad(spawn.Side, spawn.Position, spawn.Facing, spawn.Formation, spawn.Type);
            }
        }

        private TerrainMap BuildTerrain()
        {
            var terrain = new TerrainMap(scenario.TerrainMin, scenario.TerrainMax);
            foreach (ScenarioConfig.ForestSpawn forest in scenario.Forests)
            {
                terrain.AddForest(forest.Center, forest.Radius);
            }

            var points = new List<float2>();
            foreach (ScenarioConfig.RoadSpawn road in scenario.Roads)
            {
                points.Clear();
                foreach (Vector2 point in road.Points)
                {
                    points.Add(point);
                }

                terrain.AddRoad(points);
            }

            foreach (ScenarioConfig.RiverSpawn river in scenario.Rivers)
            {
                points.Clear();
                foreach (Vector2 point in river.Points)
                {
                    points.Add(point);
                }

                terrain.AddRiver(points, river.HalfWidth);
            }

            foreach (ScenarioConfig.CrossingSpawn crossing in scenario.Crossings)
            {
                terrain.AddCrossing(crossing.Position, crossing.Type);
            }

            foreach (ScenarioConfig.ForestSpawn hill in scenario.Hills)
            {
                terrain.AddHill(hill.Center, hill.Radius);
            }

            return terrain;
        }

        private void OnBuildingSelectionChanged(Building building)
        {
            buildingViews.SetSelected(building);
            // О блокгаузе рассказывает карточка внизу слева, как об отряде; у своего снизу ещё кнопка «Укрепить».
            bool blockhouse = building != null && building.Type == BuildingType.Blockhouse;
            bool ownBlockhouse = blockhouse && building.Owner == playerSide;
            buildingPanel.SetBuilding(!blockhouse || ownBlockhouse ? building : null);
            if (squadCard != null)
            {
                squadCard.SetBuilding(blockhouse ? building : null);
            }
        }

        private void OnCampSelectionChanged(UnitType? type)
        {
            buildingViews.SetSelectedCamp(playerSide, type);
            if (squadCard != null)
            {
                squadCard.SetCamp(playerSide, type);
            }

            if (campPanel != null)
            {
                campPanel.SetCamp(type);
            }
        }

        /// <summary>Выделено несколько отрядов (или снова один): кольца у всех, кнопки и карточка — на группу.</summary>
        private void OnGroupChanged(IReadOnlyList<Squad> squads)
        {
            squadViews.SetSelected(squads);
            orderPanel.SetSquads(squads);
            if (squadCard != null)
            {
                squadCard.SetSquads(squads);
            }
        }

        private void OnSelectionChanged(Squad squad)
        {
            squadViews.SetSelected(squad);
            fireArc.SetSquad(squad);
            orderPanel.SetSquad(squad);
            if (squadCard != null)
            {
                squadCard.SetSquad(squad);
            }
        }
    }
}
