using Marchlands.Bootstrap.Config;
using Marchlands.Input;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Marchlands.UI;
using Marchlands.View;
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
        [SerializeField] private int seed = 1;
        [SerializeField] private int targetFrameRate = 60;

        [Header("Сцена")]
        [SerializeField] private SquadViewRegistry squadViews;
        [SerializeField] private BattleInputController input;
        [SerializeField] private FormationToggleButton formationButton;

        private Match match;
        private FixedStepClock clock;

        public Match Match => match;

        /// <summary>Доля шага между последним выполненным тиком и следующим, в [0, 1).</summary>
        public float InterpolationAlpha => clock.Alpha;

        private void Awake()
        {
            // На мобильных Unity по умолчанию ограничивает частоту 30 кадрами.
            Application.targetFrameRate = targetFrameRate;

            match = new Match(unchecked((ulong)seed), balance.CreateSettings());
            clock = new FixedStepClock(SimulationTime.TicksPerSecond, SimulationTime.MaxTicksPerAdvance);
            SpawnScenario();

            squadViews.Initialize(match);
            input.Initialize(match, scenario.PlayerSide);
            formationButton.Initialize(match);
            input.SelectionChanged += OnSelectionChanged;
        }

        private void OnDestroy()
        {
            if (input != null)
            {
                input.SelectionChanged -= OnSelectionChanged;
            }
        }

        private void Update()
        {
            int ticks = clock.Advance(Time.deltaTime);
            for (int i = 0; i < ticks; i++)
            {
                match.Tick();
            }

            squadViews.Render(clock.Alpha);
        }

        private void SpawnScenario()
        {
            foreach (ScenarioConfig.SquadSpawn spawn in scenario.Squads)
            {
                match.AddSquad(spawn.Side, spawn.Position, spawn.Facing, spawn.Formation);
            }
        }

        private void OnSelectionChanged(Squad squad)
        {
            squadViews.SetSelected(squad);
            formationButton.SetSquad(squad);
        }
    }
}
