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
        [Tooltip("Подпись на бледной «Залп!», когда стрелять нельзя из-за своих в рукопашной с целью.")]
        [SerializeField] private string allyInMeleeText = "Свои\nв штыках";
        [Tooltip("Подпись вместо секунд перезарядки, пока отряд в рукопашной: перезарядка стоит.")]
        [SerializeField] private string fightingText = "Дерутся";

        [Header("Пушка")]
        [Tooltip("У пушки на месте «Залпа» — «Цель»: нажать и тапнуть по вражескому отряду или зданию.")]
        [SerializeField] private Sprite gunIcon;
        [SerializeField] private string gunTargetText = "Цель";
        [SerializeField] private string gunTargetingText = "Выберите\nцель";
        [Tooltip("Размер подписи заряда относительно цифр перезарядки.")]
        [SerializeField] private float gunLabelScale = 0.32f;

        [Header("Строй")]
        [SerializeField] private Button formationButton;
        [SerializeField] private Text formationLabel;
        [SerializeField] private string toColumnText = "Марш";
        [SerializeField] private string toLineText = "В линию";
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
        [Tooltip("У конницы «В атаку» стоит справа от «Стоп» с таким зазором, u экрана.")]
        [SerializeField] private float cavalryChargeGap = 16f;
        [Tooltip("У конницы — не штыки, а атака в сабли.")]
        [SerializeField] private string cavalryChargeText = "В атаку";
        [SerializeField] private string cavalryChargingText = "Рубят!";


        [Header("Стоп")]
        [SerializeField] private Button stopButton;

        [Header("Цвета")]
        [SerializeField] private Color normalColor = new Color(0.95f, 0.92f, 0.82f);
        [SerializeField] private Color activeColor = new Color(1f, 0.85f, 0.45f);

        private Match match;
        private Squad squad;
        private bool isChargeTargeting;
        private Image chargeImage;
        private Sprite volleySprite;
        private int volleyFontSize;
        private TextAnchor volleyAlignment;
        private Vector3 chargeHome;
        private bool chargeMoved;

        /// <summary>Нажата «В штыки»: нужно выбрать врага на поле.</summary>
        public event Action ChargeRequested;

        public void Initialize(Match match)
        {
            this.match = match;
            chargeImage = chargeButton.GetComponent<Image>();
            volleySprite = volleyIcon != null ? volleyIcon.sprite : null;
            volleyFontSize = volleyLabel.fontSize;
            volleyAlignment = volleyLabel.alignment;
            chargeHome = chargeButton.transform.localPosition;
            volleyButton.onClick.AddListener(OnVolley);
            formationButton.onClick.AddListener(OnFormation);
            chargeButton.onClick.AddListener(OnCharge);
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
            bool gun = squad.IsArtillery;

            // Конница не стреляет — кнопок огня у неё нет. У пушки на месте «Залпа» — выбор заряда.
            bool canFire = UnitStats.CanFire(squad, match.Settings) || gun;
            volleyButton.gameObject.SetActive(canFire);
            bool canVolley = gun ? obeys : VolleyRules.CanVolley(match, squad);
            volleyButton.interactable = canVolley;
            volleyLabel.text = gun
                ? (squad.FireState == FireState.Reloading && squad.IsInMelee ? fightingText
                    : isChargeTargeting ? gunTargetingText : gunTargetText)
                : GetVolleyText();
            // У пушки — подпись заряда мелко внизу, под картинкой; у пехоты — крупные секунды перезарядки.
            int fontSize = gun ? Mathf.RoundToInt(volleyFontSize * gunLabelScale) : volleyFontSize;
            if (volleyLabel.fontSize != fontSize)
            {
                volleyLabel.fontSize = fontSize;
                volleyLabel.alignment = gun ? TextAnchor.LowerCenter : volleyAlignment;
            }
            if (volleyIcon != null)
            {
                Sprite icon = gun && gunIcon != null ? gunIcon : volleySprite;
                if (volleyIcon.sprite != icon)
                {
                    volleyIcon.sprite = icon;
                }

                volleyIcon.color = canVolley ? volleyReadyTint : volleyPaleTint;
            }

            if (reloadFill != null)
            {
                bool reloading = squad.FireState == FireState.Reloading;
                reloadFill.enabled = reloading;
                float reloadTime = gun ? match.Settings.guns.reloadTime : match.Settings.combat.reloadTime;
                reloadFill.fillAmount = reloading && reloadTime > 0f ? 1f - squad.FireTimer / reloadTime : 0f;
                if (reloadTrack != null)
                {
                    reloadTrack.enabled = reloading;
                }
            }

            // Конница строй не меняет — кнопки строя у неё нет.
            bool cavalry = squad.Type == UnitType.Cavalry;
            if (!gun && formationButton.gameObject.activeSelf == cavalry)
            {
                formationButton.gameObject.SetActive(!cavalry);
            }

            // Пушка в атаку не ходит; у конницы «В атаку» — справа от «Стоп».
            if (chargeButton.gameObject.activeSelf == gun)
            {
                chargeButton.gameObject.SetActive(!gun);
            }

            PlaceChargeButton(cavalry);

            if (gun && formationButton.gameObject.activeSelf)
            {
                formationButton.gameObject.SetActive(false);
            }

            // Во время перестроения кнопка разворачивает его обратно — ждать уже ненужного строя не надо.
            formationButton.interactable = obeys;
            formationLabel.text = squad.TargetFormation == Formation.Line ? toColumnText : toLineText;
            if (formationIcon != null)
            {
                formationIcon.sprite = squad.TargetFormation == Formation.Line ? columnSprite : lineSprite;
            }

            bool charging = squad.Order.Type == SquadOrderType.Charge || squad.Order.Type == SquadOrderType.Assault;
            chargeButton.interactable = ChargeRules.CanCharge(squad);
            chargeLabel.text = isChargeTargeting ? chargeTargetingText
                : charging ? (cavalry ? cavalryChargingText : chargingText)
                : cavalry ? cavalryChargeText : chargeText;
            chargeImage.color = isChargeTargeting || charging ? activeColor : normalColor;

            stopButton.interactable = obeys && squad.Order.Type != SquadOrderType.None;
        }

        /// <summary>
        /// На «Залпе» секунды перезарядки; если стрелять нельзя из-за своих в рукопашной с целью — так и написано.
        /// Остальное показывают кольцо и яркость мушкета.
        /// </summary>
        private string GetVolleyText()
        {
            if (squad.FireState == FireState.Reloading)
            {
                if (squad.IsInMelee)
                {
                    return fightingText;
                }

                return Mathf.CeilToInt(squad.FireTimer).ToString();
            }

            return VolleyRules.GetBlock(match, squad) == VolleyBlock.AllyInMelee ? allyInMeleeText : string.Empty;
        }

        /// <summary>У конницы кнопка атаки встаёт справа от «Стоп» (залпа и строя у неё нет), у остальных — на своё место.</summary>
        private void PlaceChargeButton(bool cavalry)
        {
            if (cavalry == chargeMoved)
            {
                return;
            }

            chargeMoved = cavalry;
            if (!cavalry)
            {
                chargeButton.transform.localPosition = chargeHome;
                return;
            }

            var stop = (RectTransform)stopButton.transform;
            var charge = (RectTransform)chargeButton.transform;
            var stopCorners = new Vector3[4];
            stop.GetWorldCorners(stopCorners);
            var chargeCorners = new Vector3[4];
            charge.GetWorldCorners(chargeCorners);
            float scale = stop.lossyScale.x;
            float stopRight = stopCorners[2].x;
            float chargeHalf = (chargeCorners[2].x - chargeCorners[0].x) * 0.5f;
            Vector3 stopCenter = (stopCorners[0] + stopCorners[2]) * 0.5f;
            // Опорная точка кнопки может быть не в центре — ставим по центру, сохраняя смещение до опорной точки.
            Vector3 pivotOffset = charge.position - (chargeCorners[0] + chargeCorners[2]) * 0.5f;
            var center = new Vector3(stopRight + cavalryChargeGap * scale + chargeHalf, stopCenter.y, charge.position.z);
            charge.position = center + new Vector3(pivotOffset.x, pivotOffset.y, 0f);
        }

        private void OnVolley()
        {
            if (squad == null)
            {
                return;
            }

            if (squad.IsArtillery)
            {
                // Указать пушке цель — следующим тапом по врагу.
                ChargeRequested?.Invoke();
                return;
            }

            match.Enqueue(new VolleyCommand(squad.Id));
        }

        private void OnFormation()
        {
            if (squad == null)
            {
                return;
            }

            Formation next = squad.TargetFormation == Formation.Line ? Formation.Column : Formation.Line;
            match.Enqueue(new ChangeFormationCommand(squad.Id, next));
        }

        private void OnCharge()
        {
            if (squad != null)
            {
                ChargeRequested?.Invoke();
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
