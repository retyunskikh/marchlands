using System.Globalization;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>Окно характеристик рода войск: открывается кнопкой «i» у иконки найма, закрывается кнопкой или тапом.</summary>
    public sealed class UnitInfoPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text bodyLabel;
        [SerializeField] private Button closeButton;
        [SerializeField] private string grenadiersTitle = "Гренадёры";
        [SerializeField] private string jaegersTitle = "Егеря";
        [SerializeField] private string cavalryTitle = "Конница";
        [SerializeField] private string grenadiersNote = "Линейная пехота: большой отряд, крепкая мораль, силён в штыковой.";
        [SerializeField] private string jaegersNote = "Лёгкая пехота: быстры, метко бьют издалека, слабы в рукопашной. Удобны для захвата деревень.";
        [SerializeField] private string cavalryNote = "Дорогая и быстрая, страшна в атаке в сабли. Стрелять не может.";

        private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

        private Match match;

        public void Initialize(Match match)
        {
            this.match = match;
            closeButton.onClick.AddListener(Hide);
            Hide();
        }

        public void Show(UnitType type)
        {
            SimulationSettings settings = match.Settings;
            UnitTypeSettings unit = settings.Unit(type);
            bool cavalry = type == UnitType.Cavalry;
            int cost = cavalry ? settings.buildings.cavalryHireCost : settings.buildings.hireCost;
            titleLabel.text = type == UnitType.Jaegers ? jaegersTitle : cavalry ? cavalryTitle : grenadiersTitle;
            string note = type == UnitType.Jaegers ? jaegersNote : cavalry ? cavalryNote : grenadiersNote;
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
