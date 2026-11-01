using System;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Config;
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
    /// открывает характеристики, «Распустить» над иконкой отпускает ждущих этого рода войск. Вражеское здание — только
    /// название, здоровье и часовые. У повреждённого своего здания в строке состояния — здоровье и идёт ли починка. У штаба — подписанные иконки постройки казармы, конюшни и дома. Видна только при выделении.
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
        [SerializeField] private HireToggle artilleryToggle;
        [Tooltip("Иконки постройки в карточке штаба.")]
        [SerializeField] private HireToggle buildBarracksToggle;
        [SerializeField] private HireToggle buildStableToggle;
        [SerializeField] private HireToggle buildDepotToggle;
        [Tooltip("Иконка дома-пристройки в карточке штаба: +housePopulation мест.")]
        [SerializeField] private HireToggle buildHouseToggle;
        [Tooltip("Усиление казармы (казарма у стороны одна).")]
        [SerializeField] private HireToggle upgradeToggle;
        [SerializeField] private string upgradeProgressFormat = "{0}%";
        [SerializeField] private string upgradeTitle = "Усилить";
        [SerializeField] private string upgradingStatus = "Казарму усиливают — найм стоит";
        [Tooltip("Штаб и блокгауз из деревянных укрепляют в каменные.")]
        [SerializeField] private string fortifyTitle = "Укрепить";
        [Tooltip("Значки кнопки: на ней — каким здание станет (усиленная казарма, каменный штаб, каменный блокгауз).")]
        [SerializeField] private Sprite upgradeBarracksIcon;
        [SerializeField] private Sprite upgradeHeadquartersIcon;
        [SerializeField] private Sprite upgradeBlockhouseIcon;
        [SerializeField] private string headquartersFortifyingStatus = "Штаб укрепляют";
        [SerializeField] private string blockhouseFortifyingStatus = "Блокгауз укрепляют";

        [Header("Раскладка по типу здания")]
        [Tooltip("Ширина панели: у штаба пять кнопок, у казармы и конюшни — обычная, у блокгауза — одна кнопка.")]
        [SerializeField] private float headquartersPanelWidth = 1360f;
        [SerializeField] private float blockhousePanelWidth = 560f;
        [Tooltip("Шаг кнопок штаба: казарма, конюшня, депо, дом, укрепить — слева направо.")]
        [SerializeField] private float headquartersSlotStep = 265f;
        [Tooltip("Где у казармы кнопка «Усилить».")]
        [SerializeField] private float barracksUpgradeX = 270f;
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
        [SerializeField] private string depotTitle = "Артдепо";
        [SerializeField] private string artilleryName = "Пушки";
        [SerializeField] private string blockhouseTitle = "Блокгауз";
        [Tooltip("Строка своего блокгауза: {0} — ружей, {1} — дальность.")]
        [SerializeField] private string blockhouseStatusFormat = "Прикрывает проход: {0} ружей, дальность {1}";
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
        [Tooltip("Статус артдепо: пушку заказывают поштучно; каждая следующая дороже, пока предыдущие целы.")]
        [SerializeField] private string depotIdleStatus = "Пушка — по нажатию. Каждая следующая дороже";
        [SerializeField] private string depotOrderingStatus = "Готовят пушку";
        [SerializeField] private string depotQueuedStatus = "Готовят пушку, ещё {0} в очереди";
        [Tooltip("Приписка к цене пушки: сколько ещё ждут в очереди.")]
        [SerializeField] private string gunQueueFormat = "  +{0}";
        [Tooltip("Портреты для иконок найма по порядку: гренадёр, фузилёр, всадник.")]
        [SerializeField] private Sprite[] francePortraits = new Sprite[3];
        [SerializeField] private Sprite[] austriaPortraits = new Sprite[3];
        [SerializeField] private string grenadiersName = "Гренадёры";
        [SerializeField] private string fusiliersName = "Фузилёры";
        [SerializeField] private string cavalryName = "Конница";
        [Tooltip("Название вражеского здания: {0} — название.")]
        [SerializeField] private string enemyTitleFormat = "{0} противника";
        [Tooltip("Строка вражеского здания: {0} — здоровье, {1} — из скольких, {2} — часовых, {3} — их дальность.")]
        [SerializeField] private string enemyStatusFormat = "Здоровье {0}/{1} · часовые: {2} ружья, дальность {3}";
        [Tooltip("Строка повреждённого своего здания: {0} — здоровье, {1} — из скольких.")]
        [SerializeField] private string damagedFormat = "Здоровье {0}/{1}";
        [SerializeField] private string repairingText = " · чинят рабочие";

        private Match match;
        private Side playerSide;
        private Building building;

        /// <summary>В карточке штаба нажата иконка постройки здания этого типа.</summary>
        public event Action<BuildingType> BuildRequested;

        public void Initialize(Match match, Side playerSide)
        {
            this.match = match;
            this.playerSide = playerSide;
            Bind(grenadiersToggle, UnitType.Grenadiers);
            Bind(fusiliersToggle, UnitType.Fusiliers);
            Bind(cavalryToggle, UnitType.Cavalry);
            if (artilleryToggle != null)
            {
                // Пушку заказывают поштучно: одно нажатие — одна пушка.
                artilleryToggle.Button.onClick.AddListener(OnOrderGun);
                artilleryToggle.InfoButton.onClick.AddListener(() => infoPanel.Show(UnitType.Artillery, building != null ? building.Owner : playerSide));
            }

            if (buildDepotToggle != null)
            {
                buildDepotToggle.Button.onClick.AddListener(() => BuildRequested?.Invoke(BuildingType.ArtilleryDepot));
            }
            if (buildBarracksToggle != null)
            {
                buildBarracksToggle.Button.onClick.AddListener(() => BuildRequested?.Invoke(BuildingType.Barracks));
                buildStableToggle.Button.onClick.AddListener(() => BuildRequested?.Invoke(BuildingType.Stable));
            }

            if (upgradeToggle != null)
            {
                upgradeToggle.Button.onClick.AddListener(OnUpgrade);
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
            else
            {
                LayOut(building.Type);
            }

            Refresh();
        }

        /// <summary>Ширина панели и места кнопок под тип здания (у штаба добавилась кнопка «Укрепить»).</summary>
        private void LayOut(BuildingType type)
        {
            var panelRect = (RectTransform)panel.transform;
            if (defaultPanelWidth <= 0f)
            {
                defaultPanelWidth = panelRect.sizeDelta.x;
            }

            float width = type == BuildingType.Headquarters ? headquartersPanelWidth
                : type == BuildingType.Blockhouse ? blockhousePanelWidth : defaultPanelWidth;
            panelRect.sizeDelta = new Vector2(width, panelRect.sizeDelta.y);
            if (type == BuildingType.Headquarters)
            {
                SetX(buildBarracksToggle, -2f * headquartersSlotStep);
                SetX(buildStableToggle, -headquartersSlotStep);
                SetX(buildDepotToggle, 0f);
                SetX(buildHouseToggle, headquartersSlotStep);
                SetX(upgradeToggle, 2f * headquartersSlotStep);
            }
            else
            {
                SetX(upgradeToggle, type == BuildingType.Blockhouse ? 0f : barracksUpgradeX);
            }
        }

        private static void SetX(Component toggle, float x)
        {
            if (toggle == null)
            {
                return;
            }

            var rect = (RectTransform)toggle.transform;
            rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
        }

        private float defaultPanelWidth;

        private void Bind(HireToggle toggle, UnitType type)
        {
            toggle.Button.onClick.AddListener(() => OnToggle(type));
            toggle.InfoButton.onClick.AddListener(() => infoPanel.Show(type, building != null ? building.Owner : playerSide));
            if (toggle.DisbandConfirm != null)
            {
                toggle.DisbandConfirm.Confirmed += () => OnDisband(type);
            }
            else if (toggle.DisbandButton != null)
            {
                toggle.DisbandButton.onClick.AddListener(() => OnDisband(type));
            }
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

            bool own = building.Owner == playerSide;
            bool stable = building.Type == BuildingType.Stable;
            bool barracks = building.Type == BuildingType.Barracks;
            bool blockhouse = building.Type == BuildingType.Blockhouse;
            bool depot = building.Type == BuildingType.ArtilleryDepot;
            string title = stable ? stableTitle : barracks ? barracksTitle : blockhouse ? blockhouseTitle : depot ? depotTitle : headquartersTitle;
            titleLabel.text = own ? title : string.Format(enemyTitleFormat, title);
            RefreshToggle(grenadiersToggle, UnitType.Grenadiers, grenadiersName, own && barracks);
            RefreshToggle(fusiliersToggle, UnitType.Fusiliers, fusiliersName, own && barracks);
            RefreshToggle(cavalryToggle, UnitType.Cavalry, cavalryName, own && stable);
            if (artilleryToggle != null)
            {
                RefreshGunToggle(own && depot);
            }
            bool headquarters = own && building.Type == BuildingType.Headquarters;
            RefreshBuildToggle(buildBarracksToggle, BuildingType.Barracks, barracksTitle, headquarters);
            RefreshBuildToggle(buildStableToggle, BuildingType.Stable, stableTitle, headquarters);
            RefreshBuildToggle(buildDepotToggle, BuildingType.ArtilleryDepot, depotTitle, headquarters);
            RefreshHouseToggle(headquarters);
            RefreshUpgradeToggle(own && BuildRules.IsUpgradable(building.Type));
            if (!own)
            {
                statusLabel.text = GetEnemyStatus();
                return;
            }

            if (building.Health < building.MaxHealth)
            {
                statusLabel.text = GetDamagedStatus();
                return;
            }

            if (building.IsUpgrading)
            {
                statusLabel.text = barracks ? upgradingStatus
                    : blockhouse ? blockhouseFortifyingStatus : headquartersFortifyingStatus;
                return;
            }

            if (blockhouse)
            {
                Volley volley = Volley.Of(building, match.Settings.buildings);
                statusLabel.text = string.Format(blockhouseStatusFormat, volley.Shots, volley.Range);
                return;
            }

            statusLabel.text = headquarters && buildHouseToggle != null ? GetPopulationStatus()
                : headquarters && buildBarracksToggle != null ? headquartersBuildStatus
                : depot && building.IsBuilt ? (building.IsOrderingGun
                    ? (building.GunOrdersQueued > 0 ? string.Format(depotQueuedStatus, building.GunOrdersQueued) : depotOrderingStatus)
                    : depotIdleStatus)
                : GetStatus(barracks || stable || depot);
        }

        private string GetEnemyStatus()
        {
            Volley volley = Volley.Of(building, match.Settings.buildings);
            return string.Format(enemyStatusFormat, Mathf.CeilToInt(building.Health), Mathf.RoundToInt(building.MaxHealth),
                building.IsBuilt ? volley.Shots : 0, volley.Range);
        }

        private string GetDamagedStatus()
        {
            string text = string.Format(damagedFormat, Mathf.CeilToInt(building.Health), Mathf.RoundToInt(building.MaxHealth));
            return building.IsRepairing ? text + repairingText : text;
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
            toggle.SetDisband(match.GetPlayer(building.Owner).GetReserve(type));
        }

        /// <summary>Заказ пушки: цена следующей пушки; пока пушку готовят — полоска, нажать нельзя.</summary>
        private void RefreshGunToggle(bool visible)
        {
            HireToggle toggle = artilleryToggle;
            if (toggle.gameObject.activeSelf != visible)
            {
                toggle.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            Sprite[] portraits = building.Owner == Side.France ? francePortraits : austriaPortraits;
            if (portraits != null && (int)UnitType.Artillery < portraits.Length)
            {
                toggle.SetIcon(portraits[(int)UnitType.Artillery]);
            }

            bool available = BuildRules.CanOrderGun(match, building);
            toggle.Button.interactable = available;
            toggle.SetAvailable(available || building.IsOrderingGun);
            float interval = match.Settings.buildings.gunCrewHireInterval;
            float progress = building.IsOrderingGun && interval > 0f ? 1f - building.GunOrderRemaining / interval : -1f;
            string text = string.Format(toggleFormat, artilleryName, BuildRules.GetGunCost(match, building.Owner));
            if (building.GunOrdersQueued > 0)
            {
                text += string.Format(gunQueueFormat, building.GunOrdersQueued);
            }

            toggle.Show(text, building.IsOrderingGun, progress);
            toggle.SetDisband(0);
        }

        private void OnOrderGun()
        {
            if (building != null)
            {
                match.Enqueue(new OrderGunCommand(building.Id));
            }
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

            // Казарма у стороны одна: построена — значок бледный.
            bool affordable = BuildRules.CanAfford(match, building.Owner, type) && !BuildRules.IsAtLimit(match, building.Owner, type);
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

        /// <summary>«Усилить» у казармы, «Укрепить» у штаба и блокгауза: цена; пока строят — полоска с процентами; потом кнопки нет.</summary>
        private void RefreshUpgradeToggle(bool visible)
        {
            if (upgradeToggle == null)
            {
                return;
            }

            // Усиленной казарме кнопка не нужна — она исчезает.
            visible &= building.IsBuilt && !building.IsUpgraded;
            if (upgradeToggle.gameObject.activeSelf != visible)
            {
                upgradeToggle.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            int cost = BuildRules.GetUpgradeCost(match, building.Type);
            bool can = BuildRules.CanUpgrade(match, building);
            upgradeToggle.Button.interactable = can;
            upgradeToggle.SetName(building.Type == BuildingType.Barracks ? upgradeTitle : fortifyTitle);
            upgradeToggle.SetIcon(building.Type == BuildingType.Headquarters ? upgradeHeadquartersIcon
                : building.Type == BuildingType.Blockhouse ? upgradeBlockhouseIcon : upgradeBarracksIcon);
            upgradeToggle.SetAvailable(can || building.IsUpgrading);
            float time = BuildRules.GetUpgradeTime(match, building.Type);
            // Идёт усиление — полоса заполняется с первого тика (чуть видна сразу), вместо цены — проценты.
            float progress = building.IsUpgrading && time > 0f ? Mathf.Max(1f - building.UpgradeRemaining / time, UpgradeProgressMin) : -1f;
            string text = building.IsUpgrading
                ? string.Format(upgradeProgressFormat, Mathf.FloorToInt(progress * 100f))
                : string.Format(buildFormat, string.Empty, cost);
            upgradeToggle.Show(text, building.IsUpgrading, progress);
        }

        // Сколько полосы видно в первый миг усиления, чтобы было ясно, что оно пошло.
        private const float UpgradeProgressMin = 0.03f;

        private void OnUpgrade()
        {
            if (building != null)
            {
                match.Enqueue(new UpgradeBuildingCommand(building.Id));
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

        private void OnDisband(UnitType type)
        {
            if (building != null)
            {
                match.Enqueue(new DisbandReserveCommand(building.Owner, type));
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
