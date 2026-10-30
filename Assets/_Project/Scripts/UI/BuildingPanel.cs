using System;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Панель выделенного здания: название, состояние постройки или найма и иконки найма — у казармы гренадёры
    /// и фузилёры, у конюшни конница: воин своей стороны в полный рост и цена человека с монеткой. Нажатие иконки
    /// включает или выключает найм этого рода войск (включённая подсвечена, под ней — время до рекрута); «i»
    /// открывает характеристики. У штаба — подписанные иконки постройки казармы, конюшни и дома. Видна только при выделении.
    /// </summary>
    public sealed class BuildingPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField] private HireToggle grenadiersToggle;
        [FormerlySerializedAs("jaegersToggle")]
        [SerializeField] private HireToggle fusiliersToggle;
        [SerializeField] private HireToggle cavalryToggle;
        [Tooltip("Иконки постройки в карточке штаба.")]
        [SerializeField] private HireToggle buildBarracksToggle;
        [SerializeField] private HireToggle buildStableToggle;
        [Tooltip("Иконка дома-пристройки в карточке штаба: +housePopulation мест.")]
        [SerializeField] private HireToggle buildHouseToggle;
        [Tooltip("{0} — население, {1} — мест.")]
        [SerializeField] private string populationFormat = "Население: {0}/{1}";
        [Tooltip("{0} — процент постройки дома.")]
        [SerializeField] private string houseBuildingFormat = " · строится дом {0}%";
        [SerializeField] private string housesMaxedText = " · домов больше не пристроить";
        [SerializeField] private string noHousingStatus = "Найм: нет мест — пристройте дом к штабу";
        [Tooltip("{0} — название, {1} — цена.")]
        [SerializeField] private string buildFormat = "{1}";
        [SerializeField] private string headquartersBuildStatus = "Постройте казарму или конюшню";
        [SerializeField] private UnitInfoPanel infoPanel;
        [SerializeField] private string headquartersTitle = "Штаб";
        [SerializeField] private string barracksTitle = "Казарма";
        [SerializeField] private string stableTitle = "Конюшня";
        [Tooltip("Подпись иконки дома в карточке штаба.")]
        [SerializeField] private string houseTitle = "Дом";
        [SerializeField] private string headquartersStatus = "Здесь не нанимают";
        [Tooltip("{0} — процент постройки.")]
        [SerializeField] private string constructionFormat = "Строится… {0}%";
        [SerializeField] private string idleStatus = "Нажмите иконку, чтобы начать найм";
        [SerializeField] private string hiringStatus = "Идёт найм";
        [SerializeField] private string noGoldStatus = "Найм: не хватает золота";
        [SerializeField] private string fullStatus = "Найм: ждут люди на два отряда";
        [Tooltip("{0} — название, {1} — цена человека.")]
        [SerializeField] private string toggleFormat = "{1}";
        [Tooltip("Портреты для иконок найма по порядку: гренадёр, фузилёр, всадник.")]
        [SerializeField] private Sprite[] francePortraits = new Sprite[3];
        [SerializeField] private Sprite[] austriaPortraits = new Sprite[3];
        [SerializeField] private string grenadiersName = "Гренадёры";
        [SerializeField] private string fusiliersName = "Фузилёры";
        [SerializeField] private string cavalryName = "Конница";

        private Match match;
        private Building building;

        /// <summary>В карточке штаба нажата иконка постройки здания этого типа.</summary>
        public event Action<BuildingType> BuildRequested;

        public void Initialize(Match match)
        {
            this.match = match;
            Bind(grenadiersToggle, UnitType.Grenadiers);
            Bind(fusiliersToggle, UnitType.Fusiliers);
            Bind(cavalryToggle, UnitType.Cavalry);
            if (buildBarracksToggle != null)
            {
                buildBarracksToggle.Button.onClick.AddListener(() => BuildRequested?.Invoke(BuildingType.Barracks));
                buildStableToggle.Button.onClick.AddListener(() => BuildRequested?.Invoke(BuildingType.Stable));
            }

            if (buildHouseToggle != null)
            {
                buildHouseToggle.Button.onClick.AddListener(OnBuildHouse);
            }

            infoPanel.Initialize(match);
            SetBuilding(null);
        }

        public void SetBuilding(Building building)
        {
            this.building = building;
            panel.SetActive(building != null);
            if (building == null)
            {
                infoPanel.Hide();
            }

            Refresh();
        }

        private void Bind(HireToggle toggle, UnitType type)
        {
            toggle.Button.onClick.AddListener(() => OnToggle(type));
            toggle.InfoButton.onClick.AddListener(() => infoPanel.Show(type));
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (building == null)
            {
                return;
            }

            bool stable = building.Type == BuildingType.Stable;
            bool barracks = building.Type == BuildingType.Barracks;
            titleLabel.text = stable ? stableTitle : barracks ? barracksTitle : headquartersTitle;
            RefreshToggle(grenadiersToggle, UnitType.Grenadiers, grenadiersName, barracks);
            RefreshToggle(fusiliersToggle, UnitType.Fusiliers, fusiliersName, barracks);
            RefreshToggle(cavalryToggle, UnitType.Cavalry, cavalryName, stable);
            bool headquarters = building.Type == BuildingType.Headquarters;
            RefreshBuildToggle(buildBarracksToggle, BuildingType.Barracks, barracksTitle, headquarters);
            RefreshBuildToggle(buildStableToggle, BuildingType.Stable, stableTitle, headquarters);
            RefreshHouseToggle(headquarters);
            statusLabel.text = headquarters && buildHouseToggle != null ? GetPopulationStatus()
                : headquarters && buildBarracksToggle != null ? headquartersBuildStatus : GetStatus(barracks || stable);
        }

        private void RefreshToggle(HireToggle toggle, UnitType type, string unitName, bool visible)
        {
            if (toggle.gameObject.activeSelf != visible)
            {
                toggle.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            Sprite[] portraits = building.Owner == Side.France ? francePortraits : austriaPortraits;
            if (portraits != null && (int)type < portraits.Length)
            {
                toggle.SetIcon(portraits[(int)type]);
            }

            toggle.Button.interactable = building.IsBuilt;
            // Нанимают по очереди — полоска только у того, чей рекрут идёт сейчас.
            float interval = BuildRules.GetHireInterval(match, building);
            bool current = building.IsHiring(type) && BuildRules.GetUpcomingHireType(match, building) == type;
            float progress = !current ? -1f : interval > 0f ? 1f - building.HireRemaining / interval : 0f;
            int cost = BuildRules.GetHireCost(match.Settings.buildings, type);
            toggle.Show(string.Format(toggleFormat, unitName, cost), building.IsHiring(type), progress);
        }

        private void RefreshBuildToggle(HireToggle toggle, BuildingType type, string title, bool visible)
        {
            if (toggle == null)
            {
                return;
            }

            if (toggle.gameObject.activeSelf != visible)
            {
                toggle.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            bool affordable = BuildRules.CanAfford(match, building.Owner, type);
            toggle.Button.interactable = affordable;
            toggle.SetAvailable(affordable);
            toggle.SetName(title);
            toggle.Show(string.Format(buildFormat, title, BuildRules.GetCost(match, type)), false, 0f);
        }

        private string GetStatus(bool hires)
        {
            if (!hires)
            {
                return headquartersStatus;
            }

            if (!building.IsBuilt)
            {
                return string.Format(constructionFormat, Mathf.FloorToInt(building.BuildProgress * 100f));
            }

            switch (BuildRules.IsWaiting(match, building))
            {
                case HireWait.Off:
                    return idleStatus;
                case HireWait.Full:
                    return fullStatus;
                case HireWait.NoHousing:
                    return noHousingStatus;
                case HireWait.NoGold:
                    return noGoldStatus;
                default:
                    return hiringStatus;
            }
        }

        private void RefreshHouseToggle(bool headquarters)
        {
            if (buildHouseToggle == null)
            {
                return;
            }

            bool visible = headquarters && building.Houses < match.Settings.buildings.maxHouses;
            if (buildHouseToggle.gameObject.activeSelf != visible)
            {
                buildHouseToggle.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            int cost = BuildRules.GetHouseCost(match, building.Owner);
            buildHouseToggle.Button.interactable = BuildRules.CanBuildHouse(match, building.Owner);
            buildHouseToggle.SetName(houseTitle);
            buildHouseToggle.SetAvailable(building.IsBuildingHouse || match.GetPlayer(building.Owner).Gold >= cost);
            buildHouseToggle.Show(string.Format(buildFormat, string.Empty, cost), building.IsBuildingHouse, -1f);
        }

        /// <summary>Строка штаба: население из мест, строящийся дом или что домов больше не пристроить.</summary>
        private string GetPopulationStatus()
        {
            string text = string.Format(populationFormat,
                BuildRules.GetPopulation(match, building.Owner), BuildRules.GetPopulationCapacity(match, building.Owner));
            if (building.IsBuildingHouse)
            {
                float buildTime = match.Settings.buildings.houseBuildTime;
                int percent = buildTime > 0f ? Mathf.FloorToInt((1f - building.HouseBuildRemaining / buildTime) * 100f) : 100;
                return text + string.Format(houseBuildingFormat, percent);
            }

            return building.Houses >= match.Settings.buildings.maxHouses ? text + housesMaxedText : text;
        }

        private void OnBuildHouse()
        {
            if (building != null)
            {
                match.Enqueue(new BuildHouseCommand(building.Owner));
            }
        }

        private void OnToggle(UnitType type)
        {
            if (building != null)
            {
                match.Enqueue(new SetHiringCommand(building.Id, type, !building.IsHiring(type)));
            }
        }
    }
}
