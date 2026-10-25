using Marchlands.Bootstrap.Config;
using Marchlands.Input;
using Marchlands.Simulation.AI;
using Marchlands.Simulation.Commands;
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
        [SerializeField] private FireArcView fireArc;
        [SerializeField] private VolleyEffectsView volleyEffects;
        [SerializeField] private BattleInputController input;
        [SerializeField] private OrderPanel orderPanel;

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
            fireArc.Initialize(match);
            volleyEffects.Initialize(match);
            input.Initialize(match, scenario.PlayerSide);
            orderPanel.Initialize(match);
            input.SelectionChanged += OnSelectionChanged;
            input.ChargeTargetingChanged += orderPanel.SetChargeTargeting;
            orderPanel.ChargeRequested += input.BeginChargeTargeting;
        }

        private void OnDestroy()
        {
            if (input != null)
            {
                input.SelectionChanged -= OnSelectionChanged;
                if (orderPanel != null)
                {
                    input.ChargeTargetingChanged -= orderPanel.SetChargeTargeting;
                    orderPanel.ChargeRequested -= input.BeginChargeTargeting;
                }
            }
        }

        private void Update()
        {
            int ticks = clock.Advance(Time.deltaTime);
            for (int i = 0; i < ticks; i++)
            {
                match.Tick();
                volleyEffects.Play(match.LastTickVolleys);
            }

            squadViews.Render(clock.Alpha);
            fireArc.Render(clock.Alpha);
        }

        private void SpawnScenario()
        {
            match.SetRetreatPoint(Side.France, scenario.GetRetreatPoint(Side.France));
            match.SetRetreatPoint(Side.Austria, scenario.GetRetreatPoint(Side.Austria));

            foreach (Side side in new[] { Side.France, Side.Austria })
            {
                if (scenario.IsAiControlled(side))
                {
                    match.AddController(new TacticalAi(side));
                }
            }

            foreach (ScenarioConfig.SquadSpawn spawn in scenario.Squads)
            {
                Squad squad = match.AddSquad(spawn.Side, spawn.Position, spawn.Facing, spawn.Formation);
                if (spawn.FireAtWill)
                {
                    match.Enqueue(new SetFireAtWillCommand(squad.Id, true));
                }
            }
        }

        private void OnSelectionChanged(Squad squad)
        {
            squadViews.SetSelected(squad);
            fireArc.SetSquad(squad);
            orderPanel.SetSquad(squad);
        }
    }
}
