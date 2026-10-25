using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Нижняя панель приказов выделенного отряда (раздел 4 docs/design.md). Видна только при выделении.
    /// Состояние отряда только читает, приказы отдаёт командами.
    /// </summary>
    public sealed class OrderPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;

        [Header("Залп")]
        [SerializeField] private Button volleyButton;
        [SerializeField] private Text volleyLabel;
        [SerializeField] private string volleyText = "Залп!";
        [SerializeField] private string aimingText = "Целься!";
        [Tooltip("{0} — сколько секунд осталось.")]
        [SerializeField] private string reloadingText = "Заряжают… {0}";

        [Header("Строй")]
        [SerializeField] private Button formationButton;
        [SerializeField] private Text formationLabel;
        [SerializeField] private string toColumnText = "В колонну";
        [SerializeField] private string toLineText = "В линию";
        [SerializeField] private string changingText = "Перестроение…";

        [Header("Огонь по готовности")]
        [SerializeField] private Button fireAtWillButton;
        [SerializeField] private Text fireAtWillLabel;
        [SerializeField] private string fireAtWillOnText = "Огонь по готовности: вкл";
        [SerializeField] private string fireAtWillOffText = "Огонь по готовности: выкл";
        [SerializeField] private Color fireAtWillOnColor = new Color(1f, 0.85f, 0.45f);
        [SerializeField] private Color fireAtWillOffColor = new Color(0.95f, 0.92f, 0.82f);

        [Header("Стоп")]
        [SerializeField] private Button stopButton;

        private Match match;
        private Squad squad;
        private Image fireAtWillImage;

        public void Initialize(Match match)
        {
            this.match = match;
            fireAtWillImage = fireAtWillButton.GetComponent<Image>();
            volleyButton.onClick.AddListener(OnVolley);
            formationButton.onClick.AddListener(OnFormation);
            fireAtWillButton.onClick.AddListener(OnFireAtWill);
            stopButton.onClick.AddListener(OnStop);
            SetSquad(null);
        }

        public void SetSquad(Squad squad)
        {
            this.squad = squad;
            panel.SetActive(squad != null);
            Refresh();
        }

        private void Update()
        {
            if (squad != null && match.FindSquad(squad.Id) == null)
            {
                SetSquad(null);
                return;
            }

            Refresh();
        }

        private void Refresh()
        {
            if (squad == null)
            {
                return;
            }

            volleyButton.interactable = VolleyRules.CanVolley(match, squad);
            volleyLabel.text = squad.FireState switch
            {
                FireState.Aiming => aimingText,
                FireState.Reloading => string.Format(reloadingText, Mathf.CeilToInt(squad.FireTimer)),
                _ => volleyText
            };

            formationButton.interactable = !squad.IsChangingFormation;
            formationLabel.text = squad.IsChangingFormation
                ? changingText
                : squad.Formation == Formation.Line ? toColumnText : toLineText;

            fireAtWillLabel.text = squad.FireAtWill ? fireAtWillOnText : fireAtWillOffText;
            fireAtWillImage.color = squad.FireAtWill ? fireAtWillOnColor : fireAtWillOffColor;

            stopButton.interactable = squad.Order.Type == SquadOrderType.Move;
        }

        private void OnVolley()
        {
            if (squad != null)
            {
                match.Enqueue(new VolleyCommand(squad.Id));
            }
        }

        private void OnFormation()
        {
            if (squad == null || squad.IsChangingFormation)
            {
                return;
            }

            Formation next = squad.Formation == Formation.Line ? Formation.Column : Formation.Line;
            match.Enqueue(new ChangeFormationCommand(squad.Id, next));
        }

        private void OnFireAtWill()
        {
            if (squad != null)
            {
                match.Enqueue(new SetFireAtWillCommand(squad.Id, !squad.FireAtWill));
            }
        }

        private void OnStop()
        {
            if (squad != null)
            {
                match.Enqueue(new StopCommand(squad.Id));
            }
        }
    }
}
