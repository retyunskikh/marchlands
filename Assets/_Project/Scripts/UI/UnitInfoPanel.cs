using System.Globalization;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Окно характеристик рода войск: слева — большой портрет воина своей стороны, справа — описание и характеристики.
    /// Открывается кнопкой «i» у иконки найма, закрывается кнопкой или тапом.
    /// </summary>
    public sealed class UnitInfoPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text bodyLabel;
        [SerializeField] private Button closeButton;
        [Tooltip("Большой портрет воина слева.")]
        [SerializeField] private Image portrait;
        [Tooltip("Портреты по порядку: гренадёр, фузилёр, всадник.")]
        [SerializeField] private Sprite[] francePortraits = new Sprite[3];
        [SerializeField] private Sprite[] austriaPortraits = new Sprite[3];
        [SerializeField] private string grenadiersTitle = "Гренадёры";
        [SerializeField] private string fusiliersTitle = "Фузилёры";
        [SerializeField] private string cavalryTitle = "Конница";
        [SerializeField] private string grenadiersNote = "Отборная пехота: метче стреляют, крепкая мораль, сильны в штыковой. Дороже фузилёров и нанимаются вдвое дольше.";
        [SerializeField] private string fusiliersNote = "Основная линейная пехота: дёшевы и быстро набираются, но уступают гренадёрам в стойкости и штыковой.";
        [SerializeField] private string cavalryNote = "Дорогая и быстрая, страшна в атаке в сабли. Стрелять не может.";

        private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

        private Match match;

        public void Initialize(Match match)
        {
            this.match = match;
            closeButton.onClick.AddListener(Hide);
            Hide();
        }

        public void Show(UnitType type, Side side)
        {
            Sprite[] portraits = side == Side.France ? francePortraits : austriaPortraits;
            if (portrait != null && portraits != null && (int)type < portraits.Length && portraits[(int)type] != null)
            {
                portrait.sprite = portraits[(int)type];
                portrait.preserveAspect = true;
            }

            SimulationSettings settings = match.Settings;
            UnitTypeSettings unit = settings.Unit(type);
            bool cavalry = type == UnitType.Cavalry;
            int cost = BuildRules.GetHireCost(settings.buildings, type);
            titleLabel.text = type == UnitType.Fusiliers ? fusiliersTitle : cavalry ? cavalryTitle : grenadiersTitle;
            string note = type == UnitType.Fusiliers ? fusiliersNote : cavalry ? cavalryNote : grenadiersNote;
            string fire = unit.canFire
                ? $"Меткость: {Percent(unit.accuracyMultiplier)}\nДальность огня: {Percent(unit.rangeMultiplier)}"
                : "Стрелять не может";
            bodyLabel.text =
                $"{note}\n\n" +
                $"Отряд: {unit.squadSize} чел. (бежит, если меньше {unit.minSquadSize})\n" +
                $"Цена: {cost} золота за человека, {cost * unit.squadSize} за отряд\n" +
                $"Скорость: {Percent(unit.speedMultiplier)}\n" +
                $"{fire}\n" +
                $"Рукопашная: {Percent(unit.meleeMultiplier)}\n" +
                $"Стойкость: {Percent(1f / unit.moraleLossMultiplier)}";
            panel.SetActive(true);
        }

        public void Hide()
        {
            panel.SetActive(false);
        }

        /// <summary>Множитель к обычной пехоте в процентах: 1.25 → «125%».</summary>
        private static string Percent(float multiplier)
        {
            return Mathf.RoundToInt(multiplier * 100f).ToString(Russian) + "%";
        }
    }
}
