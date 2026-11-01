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
        [Tooltip("Портреты по номеру рода войск: гренадёр, фузилёр, всадник, (рекруты), пушка.")]
        [SerializeField] private Sprite[] francePortraits = new Sprite[3];
        [SerializeField] private Sprite[] austriaPortraits = new Sprite[3];
        [SerializeField] private string grenadiersTitle = "Гренадёры";
        [SerializeField] private string fusiliersTitle = "Фузилёры";
        [SerializeField] private string cavalryTitle = "Конница";
        [SerializeField] private string grenadiersNote = "Отборная пехота: метче стреляют, крепкая мораль, сильны в штыковой. Дороже фузилёров и нанимаются вдвое дольше.";
        [SerializeField] private string fusiliersNote = "Основная линейная пехота: дёшевы и быстро набираются, но уступают гренадёрам в стойкости и штыковой.";
        [SerializeField] private string cavalryNote = "Дорогая и быстрая, страшна в атаке в сабли. Стрелять не может.";
        [SerializeField] private string artilleryTitle = "Пушки";
        [SerializeField] private string artilleryNote = "Пушка с расчётом. Медленная; чтобы стрелять, снимается с передка, чтобы ехать — берётся на передок. Ядром бьёт далеко, навесом через своих, рикошетом выкашивает ряды — особенно колонну и строй на мосту. В атаку не ходит.";
        [Tooltip("Цена пушки: {0} — сколько стоит следующая пушка с расчётом, {1} — во сколько раз дороже каждая следующая.")]
        [SerializeField] private string gunCostFormat = "Цена: {0} золота за пушку с расчётом; каждая следующая в {1} раза дороже, пока прежние в строю";

        [Header("Усиление зданий")]
        [SerializeField] private string upgradeBarracksTitle = "Усилить казарму";
        [SerializeField] private string upgradeHeadquartersTitle = "Укрепить штаб";
        [SerializeField] private string upgradeBlockhouseTitle = "Укрепить блокгауз";
        [SerializeField] private string upgradeBarracksNote = "Деревянную казарму перестраивают в каменную. Пока идёт стройка, найм стоит.";
        [SerializeField] private string upgradeHeadquartersNote = "Деревянный штаб перестраивают в каменный: базу дольше не взять штурмом.";
        [SerializeField] private string upgradeBlockhouseNote = "Деревянный блокгауз перестраивают в каменный: крепче, больше защитников, бьют дальше.";
        [Tooltip("Строка «было → станет»: {0} — что, {1} — сейчас, {2} — после.")]
        [SerializeField] private string upgradeChangeFormat = "{0}: {1} → {2}";
        [SerializeField] private string upgradeHealthName = "Прочность";
        [SerializeField] private string upgradeSentriesName = "Ружей у часовых";
        [SerializeField] private string upgradeDefendersName = "Стрелков";
        [SerializeField] private string upgradeRangeName = "Дальность огня";
        [Tooltip("{0} — во сколько раз быстрее нанимают.")]
        [SerializeField] private string upgradeHireFormat = "Найм: в {0} раза быстрее";
        [Tooltip("{0} — цена, {1} — секунд стройки.")]
        [SerializeField] private string upgradeCostFormat = "Цена: {0} золота, строят {1} с";

        [Header("Воздушный шар")]
        [SerializeField] private string balloonTitle = "Воздушный шар";
        [SerializeField] private string balloonNote = "Привязной шар над штабом: наблюдатель видит всё поле. Туман войны для вас снят до конца боя — видны все отряды противника.";
        [Tooltip("{0} — цена, {1} — секунд изучения.")]
        [SerializeField] private string balloonCostFormat = "Цена: {0} золота, изучают {1} с";

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
            bool artillery = type == UnitType.Artillery;
            titleLabel.text = type == UnitType.Fusiliers ? fusiliersTitle : cavalry ? cavalryTitle : artillery ? artilleryTitle : grenadiersTitle;
            string note = type == UnitType.Fusiliers ? fusiliersNote : cavalry ? cavalryNote : artillery ? artilleryNote : grenadiersNote;
            string fire = artillery
                ? $"Ядро: {Mathf.RoundToInt(settings.guns.roundShotMinRange)}–{Mathf.RoundToInt(settings.guns.roundShotRange)} u\nПерезарядка: {Mathf.RoundToInt(settings.guns.reloadTime)} с"
                : unit.canFire
                ? $"Меткость: {Percent(unit.accuracyMultiplier)}\nДальность огня: {Percent(unit.rangeMultiplier)}"
                : "Стрелять не может";
            string crew = artillery ? $"Расчёт: {unit.squadSize} чел. (бросает пушку, если меньше {unit.minSquadSize})"
                : $"Отряд: {unit.squadSize} чел. (бежит, если меньше {unit.minSquadSize})";
            // Пушку заказывают целиком (с расчётом), и цена растёт с каждой пушкой в строю.
            string price = artillery
                ? string.Format(Russian, gunCostFormat, BuildRules.GetGunCost(match, side), Number(settings.buildings.gunCostGrowth))
                : $"Цена: {cost} золота за человека, {cost * unit.squadSize} за отряд";
            bodyLabel.text =
                $"{note}\n\n" +
                $"{crew}\n" +
                $"{price}\n" +
                $"Скорость: {Percent(unit.speedMultiplier)}\n" +
                $"{fire}\n" +
                $"Рукопашная: {Percent(unit.meleeMultiplier)}\n" +
                $"Стойкость: {Percent(1f / unit.moraleLossMultiplier)}";
            panel.SetActive(true);
        }

        /// <summary>Что даст усиление (укрепление) здания: картинка будущего здания, «было → станет», цена и время.</summary>
        public void ShowUpgrade(Building building, Sprite picture)
        {
            if (portrait != null && picture != null)
            {
                portrait.sprite = picture;
                portrait.preserveAspect = true;
            }

            BuildingSettings settings = match.Settings.buildings;
            string body;
            if (building.Type == BuildingType.Barracks)
            {
                titleLabel.text = upgradeBarracksTitle;
                body = upgradeBarracksNote + "\n\n"
                    + string.Format(Russian, upgradeHireFormat, Number(settings.barracksUpgradeHireSpeed)) + "\n"
                    + Change(upgradeSentriesName, settings.sentryShots, settings.upgradedSentryShots);
            }
            else
            {
                bool blockhouse = building.Type == BuildingType.Blockhouse;
                titleLabel.text = blockhouse ? upgradeBlockhouseTitle : upgradeHeadquartersTitle;
                float health = building.MaxHealth;
                body = (blockhouse ? upgradeBlockhouseNote : upgradeHeadquartersNote) + "\n\n"
                    + Change(upgradeHealthName, Mathf.RoundToInt(health), Mathf.RoundToInt(health * settings.fortifiedHealthMultiplier));
                body += blockhouse
                    ? "\n" + Change(upgradeDefendersName, settings.blockhouseShots, settings.upgradedBlockhouseShots)
                      + "\n" + Change(upgradeRangeName, Mathf.RoundToInt(settings.blockhouseRange), Mathf.RoundToInt(settings.upgradedBlockhouseRange))
                    : "\n" + Change(upgradeSentriesName, settings.sentryShots, settings.upgradedSentryShots);
            }

            body += "\n" + string.Format(Russian, upgradeCostFormat, BuildRules.GetUpgradeCost(match, building.Type),
                Mathf.RoundToInt(BuildRules.GetUpgradeTime(match, building.Type)));
            bodyLabel.text = body;
            panel.SetActive(true);
        }

        private string Change(string name, int now, int after)
        {
            return string.Format(Russian, upgradeChangeFormat, name, now, after);
        }

        private static string Number(float value)
        {
            return value.ToString("0.#", Russian);
        }

        /// <summary>Что даёт воздушный шар: картинка, описание, цена и время изучения.</summary>
        public void ShowBalloon(Sprite picture)
        {
            if (portrait != null && picture != null)
            {
                portrait.sprite = picture;
                portrait.preserveAspect = true;
            }

            titleLabel.text = balloonTitle;
            VisionSettings vision = match.Settings.vision;
            bodyLabel.text = balloonNote + "\n\n" + string.Format(Russian, balloonCostFormat, vision.balloonCost, Mathf.RoundToInt(vision.balloonResearchTime));
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
