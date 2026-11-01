using System.Collections.Generic;
using Marchlands.Bootstrap.Config;
using Marchlands.Input;
using Marchlands.Simulation.AI;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Commands;
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
        private GatedController enemyTactics;
        private GatedController enemyStrategy;

        public Match Match => match;

        /// <summary>Доля шага между последним выполненным тиком и следующим, в [0, 1).</summary>
        public float InterpolationAlpha => clock.Alpha;

        private void Awake()
        {
            // На мобильных Unity по умолчанию ограничивает частоту 30 кадрами.
            Application.targetFrameRate = targetFrameRate;

            isTutorial = MapChoice.Tutorial && tutorialScenario != null && tutorial != null;
            if (isTutorial)
            {
                scenario = tutorialScenario;
            }
            else if (maps != null && maps.Length > 0)
            {
                scenario = maps[Mathf.Clamp(MapChoice.Selected, 0, maps.Length - 1)];
            }

            match = new Match(unchecked((ulong)seed), balance.CreateSettings());
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
            squadViews.Initialize(match);
            villageViews.Initialize(match, scenario.VillageLayouts, scenario.GetVillageDoors);
            buildingViews.Initialize(match);
            fireArc.Initialize(match);
            volleyEffects.Initialize(match, buildingViews);
            if (rainView != null)
            {
                rainView.Initialize(match);
            }
            battleSounds.Initialize(match, scenario.PlayerSide);
            music.Initialize(match, scenario.PlayerSide);
            input.Initialize(match, scenario.PlayerSide);
            orderPanel.Initialize(match);
            if (squadCard != null)
            {
                squadCard.Initialize(match, scenario.PlayerSide);
            }
            topBar.Initialize(match, scenario.PlayerSide);
            buildPanel.Initialize(match, scenario.PlayerSide);
            buildingPanel.Initialize(match, scenario.PlayerSide);
            formSquadPanel.Initialize(match, scenario.PlayerSide);
            gameMenu.Initialize(match, scenario.PlayerSide, music);
            if (campPanel != null)
            {
                campPanel.Initialize(match, scenario.PlayerSide);
            }

            if (isTutorial && enemyTactics != null)
            {
                tutorial.Initialize(match, scenario.PlayerSide, input, enemyTactics, enemyStrategy);
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

        private void Update()
        {
            int ticks = clock.Advance(Time.deltaTime);
            for (int i = 0; i < ticks; i++)
            {
                match.Tick();
                volleyEffects.Play(match.LastTickVolleys);
                volleyEffects.PlaySentries(match.LastTickSentryVolleys);
                volleyEffects.PlayArtillery(match.LastTickArtilleryShots);
                buildingViews.PlaySentries(match.LastTickSentryVolleys);
                battleSounds.Play(match.LastTickCaptures);
                villageViews.Play(match.LastTickReinforcements);
                buildingViews.PlayDisbands(match.LastTickDisbands);
                SelectFormedSquad();
            }

            squadViews.Render(clock.Alpha);
            villageViews.Render();
            buildingViews.Render();
            fireArc.Render(clock.Alpha);
        }

        /// <summary>Только что построенный отряд игрока сразу выделяется.</summary>
        private void SelectFormedSquad()
        {
            foreach (Squad formed in match.LastTickFormedSquads)
            {
                if (formed.Side == scenario.PlayerSide)
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
                else if (scenario.IsAiControlled(side))
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
            bool ownBlockhouse = blockhouse && building.Owner == scenario.PlayerSide;
            buildingPanel.SetBuilding(!blockhouse || ownBlockhouse ? building : null);
            if (squadCard != null)
            {
                squadCard.SetBuilding(blockhouse ? building : null);
            }
        }

        private void OnCampSelectionChanged(UnitType? type)
        {
            buildingViews.SetSelectedCamp(scenario.PlayerSide, type);
            if (squadCard != null)
            {
                squadCard.SetCamp(scenario.PlayerSide, type);
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
