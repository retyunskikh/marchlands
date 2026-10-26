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
        [Tooltip("Картинка кнопки: дуло мушкета; бледнеет, когда залп невозможен.")]
        [SerializeField] private Image volleyIcon;
        [Tooltip("Круговая полоса перезарядки поверх кнопки и её тёмная дорожка.")]
        [SerializeField] private Image reloadFill;
        [SerializeField] private Image reloadTrack;
        [SerializeField] private Color volleyReadyTint = Color.white;
        [SerializeField] private Color volleyPaleTint = new Color(1f, 1f, 1f, 0.35f);

        [Header("Строй")]
        [SerializeField] private Button formationButton;
        [SerializeField] private Text formationLabel;
        [SerializeField] private string toColumnText = "В колонну";
        [SerializeField] private string toLineText = "В линию";
        [SerializeField] private string changingText = "Перестроение…";
        [SerializeField] private Image formationIcon;
        [Tooltip("Что покажет кнопка: строй, в который отряд перестроится.")]
        [SerializeField] private Sprite lineSprite;
        [SerializeField] private Sprite columnSprite;

        [Header("В штыки")]
        [SerializeField] private Button chargeButton;
        [SerializeField] private Text chargeLabel;
        [SerializeField] private string chargeText = "В штыки";
        [SerializeField] private string chargeTargetingText = "Выберите врага";
        [SerializeField] private string chargingText = "В атаку!";

        [Header("Автоматический огонь")]
        [SerializeField] private Button fireAtWillButton;
        [SerializeField] private Text fireAtWillLabel;
        [SerializeField] private string fireAtWillOnText = "Автоматический огонь: вкл";
        [SerializeField] private string fireAtWillOffText = "Автоматический огонь: выкл";

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
        private Image volleyImage;

        /// <summary>Нажата «В штыки»: нужно выбрать врага на поле.</summary>
        public event Action ChargeRequested;

        public void Initialize(Match match)
        {
            this.match = match;
            fireAtWillImage = fireAtWillButton.GetComponent<Image>();
            chargeImage = chargeButton.GetComponent<Image>();
            volleyImage = volleyButton.GetComponent<Image>();
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

            // Конница не стреляет — кнопок огня у неё нет.
            bool canFire = UnitStats.CanFire(squad, match.Settings);
            volleyButton.gameObject.SetActive(canFire);
            fireAtWillButton.gameObject.SetActive(canFire);
            bool canVolley = VolleyRules.CanVolley(match, squad);
            volleyButton.interactable = canVolley;
            volleyLabel.text = GetVolleyText();
            if (volleyIcon != null)
            {
                volleyIcon.color = canVolley ? volleyReadyTint : volleyPaleTint;
            }

            // Автоматический огонь — режим «Залпа»: включён — у «Залпа» золотая рамка.
            if (volleyImage != null)
            {
                volleyImage.color = squad.FireAtWill ? activeColor : normalColor;
            }

            if (reloadFill != null)
            {
                bool reloading = squad.FireState == FireState.Reloading;
                reloadFill.enabled = reloading;
                float reloadTime = match.Settings.combat.reloadTime;
                reloadFill.fillAmount = reloading && reloadTime > 0f ? 1f - squad.FireTimer / reloadTime : 0f;
                if (reloadTrack != null)
                {
                    reloadTrack.enabled = reloading;
                }
            }

            formationButton.interactable = obeys && !squad.IsChangingFormation;
            formationLabel.text = squad.IsChangingFormation
                ? changingText
                : squad.Formation == Formation.Line ? toColumnText : toLineText;
            if (formationIcon != null)
            {
                formationIcon.sprite = squad.TargetFormation == Formation.Line ? columnSprite : lineSprite;
            }

            bool charging = squad.Order.Type == SquadOrderType.Charge;
            chargeButton.interactable = ChargeRules.CanCharge(squad);
            chargeLabel.text = isChargeTargeting ? chargeTargetingText : charging ? chargingText : chargeText;
            chargeImage.color = isChargeTargeting || charging ? activeColor : normalColor;

            fireAtWillButton.interactable = obeys;
            fireAtWillLabel.text = squad.FireAtWill ? fireAtWillOnText : fireAtWillOffText;
            fireAtWillImage.color = squad.FireAtWill ? activeColor : normalColor;

            stopButton.interactable = obeys && squad.Order.Type != SquadOrderType.None;
        }

        /// <summary>На «Залпе» только секунды перезарядки; остальное показывают кольцо и яркость мушкета.</summary>
        private string GetVolleyText()
        {
            return squad.FireState == FireState.Reloading ? Mathf.CeilToInt(squad.FireTimer).ToString() : string.Empty;
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
