using Marchlands.Simulation.Core;
using Marchlands.Simulation.Morale;
using Marchlands.Simulation.Squads;
using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Карточка выделенного отряда слева внизу: воин рода войск в полный рост, название, численность (значок
    /// человечка и число), боевой дух полоской и числом и что отряд сейчас делает. Видна только при выделении.
    /// Для выделенной группы толпы рекрутов — то же место, но с пометкой, что это не отряд: сколько ждут из нужных
    /// на отряд, без боевого духа.
    /// </summary>
    public sealed class SquadCard : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Image portrait;
        [SerializeField] private Text titleLabel;
        [Tooltip("Число людей рядом со значком человечка.")]
        [SerializeField] private Text strengthLabel;
        [SerializeField] private Text detailsLabel;
        [SerializeField] private Image moraleFill;
        [Tooltip("Портреты по порядку: гренадёр, фузилёр, всадник.")]
        [SerializeField] private Sprite[] francePortraits = new Sprite[3];
        [SerializeField] private Sprite[] austriaPortraits = new Sprite[3];
        [SerializeField] private string grenadiersTitle = "Гренадёры";
        [SerializeField] private string fusiliersTitle = "Фузилёры";
        [SerializeField] private string cavalryTitle = "Конница";
        [Tooltip("{0} — живых, {1} — полная численность.")]
        [SerializeField] private string strengthFormat = "{0}/{1}";
        [Tooltip("{0} — дух числом.")]
        [SerializeField] private string moraleFormat = "Дух: {0}";
        [SerializeField] private Color moraleHigh = new Color(0.35f, 0.8f, 0.3f);
        [SerializeField] private Color moraleLow = new Color(0.9f, 0.25f, 0.2f);
        [SerializeField] private string standingText = "Стоят";
        [SerializeField] private string marchingText = "На марше";
        [SerializeField] private string chargingText = "Атакуют!";
        [SerializeField] private string meleeText = "Дерутся";
        [SerializeField] private string routingText = "Бегут";
        [SerializeField] private string formingText = "Строятся";
        [SerializeField] private string reinforcingText = "Пополняются";

        [Header("Толпа рекрутов")]
        [Tooltip("Заголовок группы толпы: {0} — род войск.")]
        [SerializeField] private string campTitleFormat = "{0}";
        [SerializeField] private string campBarracksText = "Не отряд:\nждут\nу казармы";
        [SerializeField] private string campStableText = "Не отряд:\nждут\nу конюшни";
        [Tooltip("Цвет рамки карточки толпы: отличает её от карточки отряда.")]
        [SerializeField] private Color campTint = new Color(0.82f, 0.78f, 0.7f);

        private Match match;
        private Squad squad;
        private Side campSide;
        private UnitType? campType;
        private Image panelImage;
        private Color panelColor;

        public void Initialize(Match match)
        {
            this.match = match;
            panelImage = panel.GetComponent<Image>();
            panelColor = panelImage != null ? panelImage.color : Color.white;
            SetSquad(null);
        }

        public void SetSquad(Squad squad)
        {
            this.squad = squad;
            if (squad != null)
            {
                campType = null;
            }

            panel.SetActive(squad != null || campType.HasValue);
            SetCampLook(false);
            Refresh();
        }

        /// <summary>Карточка группы толпы рекрутов этого рода войск; null — скрыть (если не выделен отряд).</summary>
        public void SetCamp(Side side, UnitType? type)
        {
            campSide = side;
            campType = type;
            if (type.HasValue)
            {
                squad = null;
            }

            panel.SetActive(squad != null || type.HasValue);
            SetCampLook(type.HasValue);
            Refresh();
        }

        private void SetCampLook(bool camp)
        {
            if (moraleFill != null && moraleFill.transform.parent != null && moraleFill.transform.parent != panel.transform)
            {
                moraleFill.transform.parent.gameObject.SetActive(!camp);
            }

            if (panelImage != null)
            {
                panelImage.color = camp ? panelColor * campTint : panelColor;
            }
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (match == null)
            {
                return;
            }

            if (squad == null && campType.HasValue)
            {
                RefreshCamp(campType.Value);
                return;
            }

            if (squad == null)
            {
                return;
            }

            if (squad.Alive <= 0)
            {
                SetSquad(null);
                return;
            }

            SetPortrait(squad.Side, squad.Type);
            titleLabel.text = GetTitle(squad.Type);

            float morale = Mathf.Clamp(squad.Morale, 0f, MoraleMath.MaxMorale);
            float share = morale / MoraleMath.MaxMorale;
            if (moraleFill != null)
            {
                moraleFill.fillAmount = share;
                moraleFill.color = Color.Lerp(moraleLow, moraleHigh, share);
            }

            strengthLabel.text = string.Format(strengthFormat, squad.Alive, UnitStats.Of(squad, match.Settings).squadSize);
            // Перезарядку показывает кнопка «Залп» — в карточке её нет.
            detailsLabel.text = string.Format(moraleFormat, Mathf.RoundToInt(morale)) + "\n" + GetActivity();
        }

        private void RefreshCamp(UnitType type)
        {
            SetPortrait(campSide, type);
            titleLabel.text = string.Format(campTitleFormat, GetTitle(type));
            int waiting = match.GetPlayer(campSide).GetReserve(type);
            strengthLabel.text = string.Format(strengthFormat, waiting, match.Settings.Unit(type).squadSize);
            detailsLabel.text = type == UnitType.Cavalry ? campStableText : campBarracksText;
        }

        private void SetPortrait(Side side, UnitType type)
        {
            Sprite[] portraits = side == Side.France ? francePortraits : austriaPortraits;
            int index = (int)type;
            if (portraits != null && index < portraits.Length && portraits[index] != null && portrait.sprite != portraits[index])
            {
                portrait.sprite = portraits[index];
            }
        }

        private string GetTitle(UnitType type)
        {
            return type == UnitType.Fusiliers ? fusiliersTitle : type == UnitType.Cavalry ? cavalryTitle : grenadiersTitle;
        }

        private string GetActivity()
        {
            if (squad.IsRouting)
            {
                return routingText;
            }

            if (squad.IsInMelee)
            {
                return meleeText;
            }

            if (squad.IsChangingFormation)
            {
                return formingText;
            }

            switch (squad.Order.Type)
            {
                case SquadOrderType.Charge:
                case SquadOrderType.Assault:
                    return chargingText;
                case SquadOrderType.Move:
                    return marchingText;
                default:
                    return squad.IsReinforcing ? reinforcingText : standingText;
            }
        }
    }
}
