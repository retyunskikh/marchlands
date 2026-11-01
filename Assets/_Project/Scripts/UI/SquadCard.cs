using System.Collections.Generic;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;
using Marchlands.View;
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
        [SerializeField] private string artilleryTitle = "Пушка";
        [SerializeField] private string limberingText = "Берут на передок";
        [SerializeField] private string unlimberingText = "Снимают с передка";
        [SerializeField] private string limberedText = "На передке";
        [SerializeField] private string gunReloadingText = "Заряжают";
        [SerializeField] private string gunReadyText = "Готова к выстрелу";
        [SerializeField] private string gunAimSquadText = "Наводят на отряд";
        [SerializeField] private string gunAimBuildingText = "Наводят на здание";
        [Tooltip("{0} — живых, {1} — полная численность.")]
        [SerializeField] private string strengthFormat = "{0}/{1}";
        [Tooltip("{0} — дух числом.")]
        [SerializeField] private string moraleFormat = "Дух: {0}";
        [Tooltip("Опытный отряд: {0} — дух, {1} — предел, {2} — знак, {3} — убито врагов.")]
        [SerializeField] private string veteranMoraleFormat = "Дух: {0}/{1}, {2} (убито {3})";
        [Tooltip("Дух выше 100, но знака ещё нет: {0} — дух, {1} — предел.")]
        [SerializeField] private string raisedMoraleFormat = "Дух: {0}/{1}";
        [SerializeField] private string[] veteranRankNames = { "бронзовая звезда", "серебряная звезда", "орден" };
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
        [SerializeField] private string campBarracksText = "Не отряд:\nждут у казармы";
        [SerializeField] private string campStableText = "Не отряд:\nждут у конюшни";
        [Tooltip("Цвет рамки карточки толпы: отличает её от карточки отряда.")]
        [SerializeField] private Color campTint = new Color(0.82f, 0.78f, 0.7f);

        [Header("Группа")]
        [Tooltip("Выделено несколько отрядов: {0} — сколько.")]
        [SerializeField] private string groupTitleFormat = "Выделено: {0}";
        [Tooltip("{0} — худший дух в группе.")]
        [SerializeField] private string groupDetailsFormat = "Худший дух: {0}";

        [Header("Блокгауз")]
        [Tooltip("Карточка блокгауза: здоровье полоской, стрелки и дальность.")]
        [SerializeField] private Sprite blockhousePortrait;
        [SerializeField] private string blockhouseTitle = "Блокгауз";
        [SerializeField] private string enemyBlockhouseTitle = "Блокгауз врага";
        [Tooltip("Значок человечка у численности — у здания его нет.")]
        [SerializeField] private GameObject strengthIcon;
        [SerializeField] private string healthFormat = "Прочность: {0}%";
        [SerializeField] private string blockhouseDetailsFormat = "Стрелков: {0}";
        [SerializeField] private string repairingSuffix = "\nЧинят";
        [SerializeField] private string rebuildingFormat = "Строят: {0}%";

        private Match match;
        private Squad squad;
        private Building building;
        private readonly List<Squad> group = new List<Squad>();
        private Side playerSide;
        private Side campSide;
        private UnitType? campType;
        private Image panelImage;
        private Color panelColor;

        public void Initialize(Match match, Side playerSide)
        {
            this.match = match;
            this.playerSide = playerSide;
            panelImage = panel.GetComponent<Image>();
            panelColor = panelImage != null ? panelImage.color : Color.white;
            SetSquad(null);
        }

        /// <summary>Выделенные отряды: один — его карточка, несколько — сводка (первый — главный).</summary>
        public void SetSquads(IReadOnlyList<Squad> squads)
        {
            group.Clear();
            group.AddRange(squads);
            SetSquad(group.Count > 0 ? group[0] : null, keepGroup: true);
        }

        public void SetSquad(Squad squad)
        {
            SetSquad(squad, keepGroup: false);
        }

        private void SetSquad(Squad squad, bool keepGroup)
        {
            if (!keepGroup)
            {
                group.Clear();
            }

            this.squad = squad;
            if (squad != null)
            {
                campType = null;
                building = null;
            }

            panel.SetActive(squad != null || campType.HasValue || building != null);
            SetCampLook(false);
            Refresh();
        }

        /// <summary>Карточка блокгауза (своего или вражеского); null — скрыть, если не выделено другое.</summary>
        public void SetBuilding(Building building)
        {
            this.building = building;
            if (building != null)
            {
                squad = null;
                campType = null;
            }

            panel.SetActive(squad != null || campType.HasValue || building != null);
            SetCampLook(false);
            Refresh();
        }

        private void LateUpdate()
        {
            bool showIcon = building == null;
            if (strengthIcon != null && strengthIcon.activeSelf != showIcon)
            {
                strengthIcon.SetActive(showIcon);
                // У здания значка нет — надпись прочности встаёт с левого края текстов карточки и шире.
                RectTransform label = strengthLabel.rectTransform;
                if (!strengthLayoutSaved)
                {
                    strengthPosition = label.anchoredPosition;
                    strengthSize = label.sizeDelta;
                    strengthLayoutSaved = true;
                }

                RectTransform title = titleLabel.rectTransform;
                label.anchoredPosition = showIcon ? strengthPosition : new Vector2(title.anchoredPosition.x, strengthPosition.y);
                label.sizeDelta = showIcon ? strengthSize : new Vector2(title.sizeDelta.x, strengthSize.y);
            }
        }

        private bool strengthLayoutSaved;
        private Vector2 strengthPosition;
        private Vector2 strengthSize;

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

            if (building != null)
            {
                RefreshBuilding();
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

            if (group.Count > 1)
            {
                RefreshGroup();
                return;
            }

            SetPortrait(squad.Side, squad.Type);
            titleLabel.text = GetTitle(squad.Type);

            float morale = Mathf.Clamp(squad.Morale, 0f, squad.MaxMorale);
            float share = morale / Mathf.Max(1f, squad.MaxMorale);
            if (moraleFill != null)
            {
                moraleFill.fillAmount = share;
                moraleFill.color = Color.Lerp(moraleLow, moraleHigh, share);
            }

            strengthLabel.text = string.Format(strengthFormat, squad.Alive, UnitStats.Of(squad, match.Settings).squadSize);
            // Перезарядку показывает кнопка «Залп» — в карточке её нет.
            detailsLabel.text = MoraleText(squad, morale) + "\n" + GetActivity();
        }

        /// <summary>Дух числом; у опытного отряда — ещё предел, знак и сколько врагов убито.</summary>
        private string MoraleText(Squad squad, float morale)
        {
            int kills = Mathf.FloorToInt(squad.Kills);
            if (kills <= 0 || squad.MaxMorale < MoraleMath.MaxMorale + 0.5f)
            {
                return string.Format(moraleFormat, Mathf.RoundToInt(morale));
            }

            int rank = squad.VeteranRank;
            if (rank <= 0 || veteranRankNames == null || rank > veteranRankNames.Length)
            {
                return string.Format(raisedMoraleFormat, Mathf.RoundToInt(morale), Mathf.RoundToInt(squad.MaxMorale));
            }

            string rankName = veteranRankNames[rank - 1];
            return string.Format(veteranMoraleFormat, Mathf.RoundToInt(morale), Mathf.RoundToInt(squad.MaxMorale), rankName, kills);
        }

        /// <summary>Сводка группы: портрет главного, сколько выделено, всего людей и худший дух.</summary>
        private void RefreshGroup()
        {
            SetPortrait(squad.Side, squad.Type);
            titleLabel.text = string.Format(groupTitleFormat, group.Count);
            int alive = 0;
            int full = 0;
            float worst = float.MaxValue;
            float share = 1f;
            foreach (Squad member in group)
            {
                alive += member.Alive;
                full += UnitStats.Of(member, match.Settings).squadSize;
                worst = Mathf.Min(worst, member.Morale);
                share = Mathf.Min(share, Mathf.Clamp01(member.Morale / Mathf.Max(1f, member.MaxMorale)));
            }

            float morale = Mathf.Max(0f, worst);
            if (moraleFill != null)
            {
                moraleFill.fillAmount = share;
                moraleFill.color = Color.Lerp(moraleLow, moraleHigh, share);
            }

            strengthLabel.text = string.Format(strengthFormat, alive, full);
            detailsLabel.text = string.Format(groupDetailsFormat, Mathf.RoundToInt(morale));
        }

        private void RefreshBuilding()
        {
            if (match.FindBuilding(building.Id) == null)
            {
                SetBuilding(null);
                return;
            }

            if (blockhousePortrait != null && portrait.sprite != blockhousePortrait)
            {
                portrait.sprite = blockhousePortrait;
            }

            titleLabel.text = building.Owner == playerSide ? blockhouseTitle : enemyBlockhouseTitle;
            float share = building.MaxHealth > 0f ? Mathf.Clamp01(building.Health / building.MaxHealth) : 0f;
            if (moraleFill != null)
            {
                moraleFill.fillAmount = share;
                moraleFill.color = Color.Lerp(moraleLow, moraleHigh, share);
            }

            // Здание — не отряд: вместо «сколько людей» — прочность в процентах, без значка человечка.
            strengthLabel.text = string.Format(healthFormat, Mathf.CeilToInt(share * 100f));
            if (!building.IsBuilt)
            {
                detailsLabel.text = string.Format(rebuildingFormat, Mathf.FloorToInt(building.BuildProgress * 100f));
                return;
            }

            Volley volley = Volley.Of(building, match.Settings.buildings);
            detailsLabel.text = string.Format(blockhouseDetailsFormat, volley.Shots)
                + (building.IsRepairing ? repairingSuffix : string.Empty);
        }

        private void RefreshCamp(UnitType type)
        {
            SetPortrait(campSide, type);
            titleLabel.text = string.Format(campTitleFormat, GetTitle(type));
            int waiting = match.GetPlayer(campSide).GetReserve(type);
            // Толпа — не отряд: просто сколько людей ждут, без «из скольких».
            strengthLabel.text = waiting.ToString();
            detailsLabel.text = type == UnitType.Cavalry ? campStableText : campBarracksText;
        }

        private void SetPortrait(Side side, UnitType type)
        {
            Sprite[] portraits = Armies.NationOf(side) == Nation.France ? francePortraits : austriaPortraits;
            int index = (int)type;
            if (portraits != null && index < portraits.Length && portraits[index] != null && portrait.sprite != portraits[index])
            {
                portrait.sprite = portraits[index];
            }
        }

        private string GetTitle(UnitType type)
        {
            return type == UnitType.Fusiliers ? fusiliersTitle : type == UnitType.Cavalry ? cavalryTitle
                : type == UnitType.Artillery ? artilleryTitle : grenadiersTitle;
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

            if (squad.IsArtillery)
            {
                // Пушка: что делает расчёт — везёт, снимает с передка, заряжает, наводит.
                if (squad.IsLimbering)
                {
                    return squad.IsLimbered ? unlimberingText : limberingText;
                }

                if (squad.Order.Type == SquadOrderType.Move)
                {
                    return marchingText;
                }

                if (squad.IsLimbered)
                {
                    return limberedText;
                }

                if (squad.FireState == Marchlands.Simulation.Combat.FireState.Reloading)
                {
                    return gunReloadingText;
                }

                if (Marchlands.Simulation.Combat.GunTargeting.TryGetTarget(match, squad, out Squad targetSquad, out Building targetBuilding))
                {
                    return targetBuilding != null ? gunAimBuildingText : gunAimSquadText;
                }

                return gunReadyText;
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
