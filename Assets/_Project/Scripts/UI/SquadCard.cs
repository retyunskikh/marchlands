using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Morale;
using Marchlands.Simulation.Squads;
using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Карточка выделенного отряда слева внизу: воин рода войск в полный рост, название, численность,
    /// боевой дух полоской и словом и что отряд сейчас делает. Видна только при выделении.
    /// </summary>
    public sealed class SquadCard : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Image portrait;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text detailsLabel;
        [SerializeField] private Image moraleFill;
        [Tooltip("Портреты по порядку: гренадёр, фузилёр, всадник.")]
        [SerializeField] private Sprite[] francePortraits = new Sprite[3];
        [SerializeField] private Sprite[] austriaPortraits = new Sprite[3];
        [SerializeField] private string grenadiersTitle = "Гренадёры";
        [SerializeField] private string fusiliersTitle = "Фузилёры";
        [SerializeField] private string cavalryTitle = "Конница";
        [Tooltip("{0} — живых, {1} — полная численность.")]
        [SerializeField] private string strengthFormat = "Численность: {0}/{1}";
        [Tooltip("{0} — дух числом, {1} — словом.")]
        [SerializeField] private string moraleFormat = "Дух: {0} — {1}";
        [Tooltip("Слова для духа: от высокого к низкому; пороги — в moraleWordThresholds.")]
        [SerializeField] private string[] moraleWords = { "твёрд", "держится", "колеблется", "на грани" };
        [SerializeField] private float[] moraleWordThresholds = { 75f, 50f, 30f };
        [SerializeField] private string routingWord = "бегут!";
        [SerializeField] private Color moraleHigh = new Color(0.35f, 0.8f, 0.3f);
        [SerializeField] private Color moraleLow = new Color(0.9f, 0.25f, 0.2f);
        [SerializeField] private string standingText = "Стоят";
        [SerializeField] private string marchingText = "На марше";
        [SerializeField] private string chargingText = "Атакуют!";
        [SerializeField] private string meleeText = "В рукопашной";
        [SerializeField] private string routingText = "Бегут";
        [SerializeField] private string formingText = "Перестраиваются";
        [SerializeField] private string aimingText = "Целься!";
        [Tooltip("{0} — секунд до готовности.")]
        [SerializeField] private string reloadingFormat = "Заряжают — {0} с";
        [SerializeField] private string loadedText = "Заряжены";
        [SerializeField] private string reinforcingText = "Пополняются в деревне";

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

            string details = string.Format(strengthFormat, squad.Alive, UnitStats.Of(squad, match.Settings).squadSize)
                + "\n" + string.Format(moraleFormat, Mathf.RoundToInt(morale), GetMoraleWord(morale))
                + "\n" + GetActivity();
            string fire = GetFireState();
            if (fire.Length > 0)
            {
                details += "\n" + fire;
            }

            detailsLabel.text = details;
        }

        private string GetMoraleWord(float morale)
        {
            if (squad.IsRouting)
            {
                return routingWord;
            }

            for (int i = 0; i < moraleWordThresholds.Length && i < moraleWords.Length; i++)
            {
                if (morale >= moraleWordThresholds[i])
                {
                    return moraleWords[i];
                }
            }

            return moraleWords.Length > 0 ? moraleWords[moraleWords.Length - 1] : string.Empty;
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
                    return chargingText;
                case SquadOrderType.Move:
                    return marchingText;
                default:
                    return squad.IsReinforcing ? reinforcingText : standingText;
            }
        }

        private string GetFireState()
        {
            if (!UnitStats.CanFire(squad, match.Settings) || squad.IsRouting)
            {
                return string.Empty;
            }

            switch (squad.FireState)
            {
                case FireState.Aiming:
                    return aimingText;
                case FireState.Reloading:
                    return string.Format(reloadingFormat, Mathf.CeilToInt(squad.FireTimer));
                default:
                    return loadedText;
            }
        }
    }
}
