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

        private Match match;
        private Squad squad;

        public void Initialize(Match match)
        {
            this.match = match;
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
            Refresh();
        }

        private void Refresh()
        {
            if (squad == null || match == null)
            {
                return;
            }

            if (squad.Alive <= 0)
            {
                SetSquad(null);
                return;
            }

            Sprite[] portraits = squad.Side == Side.France ? francePortraits : austriaPortraits;
            int index = (int)squad.Type;
            if (portraits != null && index < portraits.Length && portraits[index] != null && portrait.sprite != portraits[index])
            {
                portrait.sprite = portraits[index];
            }

            titleLabel.text = squad.Type == UnitType.Fusiliers ? fusiliersTitle : squad.Type == UnitType.Cavalry ? cavalryTitle : grenadiersTitle;

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
