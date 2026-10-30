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
        [SerializeField] private int seed = 1;
        [SerializeField] private int targetFrameRate = 60;

        [Header("Сцена")]
        [SerializeField] private TerrainView terrainView;
        [SerializeField] private SquadViewRegistry squadViews;
        [SerializeField] private VillageViewRegistry villageViews;
        [SerializeField] private BuildingViewRegistry buildingViews;
        [SerializeField] private FireArcView fireArc;
        [SerializeField] private VolleyEffectsView volleyEffects;
        [SerializeField] private BattleSoundsView battleSounds;
        [SerializeField] private MusicDirector music;
        [SerializeField] private BattleInputController input;
        [SerializeField] private OrderPanel orderPanel;
        [SerializeField] private SquadCard squadCard;
        [SerializeField] private TopBar topBar;
        [SerializeField] private BuildPanel buildPanel;
        [SerializeField] private BuildingPanel buildingPanel;
        [SerializeField] private FormSquadPanel formSquadPanel;
        [SerializeField] private GameMenu gameMenu;

        private Match match;
        private FixedStepClock clock;

        public Match Match => match;

        /// <summary>Доля шага между последним выполненным тиком и следующим, в [0, 1).</summary>
        public float InterpolationAlpha => clock.Alpha;

        private void Awake()
        {
            // На мобильных Unity по умолчанию ограничивает частоту 30 кадрами.
            Application.targetFrameRate = targetFrameRate;

            if (maps != null && maps.Length > 0)
            {
                scenario = maps[Mathf.Clamp(MapChoice.Selected, 0, maps.Length - 1)];
            }

            match = new Match(unchecked((ulong)seed), balance.CreateSettings());
            clock = new FixedStepClock(SimulationTime.TicksPerSecond, SimulationTime.MaxTicksPerAdvance);
            SpawnScenario();

            terrainView.Initialize(match);
            squadViews.Initialize(match);
            villageViews.Initialize(match, scenario.VillageLayouts, scenario.GetVillageDoors);
            buildingViews.Initialize(match);
            fireArc.Initialize(match);
            volleyEffects.Initialize(match);
            battleSounds.Initialize(match, scenario.PlayerSide);
            music.Initialize(match, scenario.PlayerSide);
            input.Initialize(match, scenario.PlayerSide);
            orderPanel.Initialize(match);
            if (squadCard != null)
            {
                squadCard.Initialize(match);
            }
            topBar.Initialize(match, scenario.PlayerSide);
            buildPanel.Initialize(match, scenario.PlayerSide);
            buildingPanel.Initialize(match);
            formSquadPanel.Initialize(match, scenario.PlayerSide);
            gameMenu.Initialize(match, scenario.PlayerSide);

            input.SelectionChanged += OnSelectionChanged;
            input.BuildingSelectionChanged += OnBuildingSelectionChanged;
            input.ChargeTargetingChanged += orderPanel.SetChargeTargeting;
            input.PlacementChanged += buildPanel.SetPlacementState;
            orderPanel.ChargeRequested += input.BeginChargeTargeting;
            buildingPanel.BuildRequested += input.BeginPlacement;
            buildPanel.ConfirmRequested += input.ConfirmPlacement;
            buildPanel.CancelRequested += input.CancelPlacement;
        }

        private void OnDestroy()
        {
            if (input == null)
            {
                return;
            }

            input.SelectionChanged -= OnSelectionChanged;
            input.BuildingSelectionChanged -= OnBuildingSelectionChanged;
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
                battleSounds.Play(match.LastTickCaptures);
                villageViews.Play(match.LastTickReinforcements);
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

                if (scenario.IsAiControlled(side))
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

            return terrain;
        }

        private void OnBuildingSelectionChanged(Building building)
        {
            buildingViews.SetSelected(building);
            buildingPanel.SetBuilding(building);
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
