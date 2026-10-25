using System;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Melee;
using Marchlands.Simulation.Squads;
using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Нижняя панель приказов выделенного отряда (раздел 4 docs/design.md). Видна только при выделении.
    /// Состояние отряда только читает, приказы отдаёт командами. Бегущий или дерущийся отряд приказов не принимает.
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
        [SerializeField] private string routingText = "Бегут!";
        [SerializeField] private string meleeText = "Рукопашная";

        [Header("Строй")]
        [SerializeField] private Button formationButton;
        [SerializeField] private Text formationLabel;
        [SerializeField] private string toColumnText = "В колонну";
        [SerializeField] private string toLineText = "В линию";
        [SerializeField] private string changingText = "Перестроение…";

        [Header("В штыки")]
        [SerializeField] private Button chargeButton;
        [SerializeField] private Text chargeLabel;
        [SerializeField] private string chargeText = "В штыки";
        [SerializeField] private string chargeTargetingText = "Выберите врага";
        [SerializeField] private string chargingText = "В атаку!";

        [Header("Огонь по готовности")]
        [SerializeField] private Button fireAtWillButton;
        [SerializeField] private Text fireAtWillLabel;
        [SerializeField] private string fireAtWillOnText = "Огонь по готовности: вкл";
        [SerializeField] private string fireAtWillOffText = "Огонь по готовности: выкл";

        [Header("Стоп")]
        [SerializeField] private Button stopButton;

        [Header("Цвета")]
        [SerializeField] private Color normalColor = new Color(0.95f, 0.92f, 0.82f);
        [SerializeField] private Color activeColor = new Color(1f, 0.85f, 0.45f);

        private Match match;
        private Squad squad;
        private bool isChargeTargeting;
        private Image fireAtWillImage;
        private Image chargeImage;

        /// <summary>Нажата «В штыки»: нужно выбрать врага на поле.</summary>
        public event Action ChargeRequested;

        public void Initialize(Match match)
        {
            this.match = match;
            fireAtWillImage = fireAtWillButton.GetComponent<Image>();
            chargeImage = chargeButton.GetComponent<Image>();
            volleyButton.onClick.AddListener(OnVolley);
            formationButton.onClick.AddListener(OnFormation);
            chargeButton.onClick.AddListener(OnCharge);
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

        public void SetChargeTargeting(bool targeting)
        {
            isChargeTargeting = targeting;
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

            bool obeys = squad.CanReceiveOrders;

            volleyButton.interactable = VolleyRules.CanVolley(match, squad);
            volleyLabel.text = GetVolleyText();

            formationButton.interactable = obeys && !squad.IsChangingFormation;
            formationLabel.text = squad.IsChangingFormation
                ? changingText
                : squad.Formation == Formation.Line ? toColumnText : toLineText;

            bool charging = squad.Order.Type == SquadOrderType.Charge;
            chargeButton.interactable = ChargeRules.CanCharge(squad);
            chargeLabel.text = isChargeTargeting ? chargeTargetingText : charging ? chargingText : chargeText;
            chargeImage.color = isChargeTargeting || charging ? activeColor : normalColor;

            fireAtWillButton.interactable = obeys;
            fireAtWillLabel.text = squad.FireAtWill ? fireAtWillOnText : fireAtWillOffText;
            fireAtWillImage.color = squad.FireAtWill ? activeColor : normalColor;

            stopButton.interactable = obeys && squad.Order.Type != SquadOrderType.None;
        }

        private string GetVolleyText()
        {
            if (squad.IsRouting)
            {
                return routingText;
            }

            if (squad.IsInMelee)
            {
                return meleeText;
            }

            return squad.FireState switch
            {
                FireState.Aiming => aimingText,
                FireState.Reloading => string.Format(reloadingText, Mathf.CeilToInt(squad.FireTimer)),
                _ => volleyText
            };
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

        private void OnCharge()
        {
            if (squad != null)
            {
                ChargeRequested?.Invoke();
            }
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
