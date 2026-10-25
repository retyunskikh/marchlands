using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Панель выделенного здания: название, состояние постройки или найма, у казармы и конюшни — кнопки
    /// «Начать / Остановить найм» и цель найма («до егерей», «до гренадёров», «без остановки»). Видна только при выделении.
    /// </summary>
    public sealed class BuildingPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Button recruitButton;
        [SerializeField] private Text recruitLabel;
        [SerializeField] private Button targetButton;
        [SerializeField] private Text targetLabel;
        [SerializeField] private string headquartersTitle = "Штаб";
        [SerializeField] private string barracksTitle = "Казарма";
        [SerializeField] private string stableTitle = "Конюшня";
        [SerializeField] private string headquartersStatus = "Здесь не нанимают";
        [Tooltip("{0} — процент постройки.")]
        [SerializeField] private string constructionFormat = "Строится… {0}%";
        [SerializeField] private string idleStatus = "Найм остановлен";
        [SerializeField] private string hiringStatus = "Идёт найм";
        [SerializeField] private string noGoldStatus = "Найм: не хватает золота";
        [SerializeField] private string fullStatus = "Найм: у зданий нет места";
        [SerializeField] private string squadReadyStatus = "Найм: людей хватает на отряд";
        [Tooltip("{0} — цена одного человека.")]
        [SerializeField] private string startHiringFormat = "Начать найм ({0} за чел.)";
        [SerializeField] private string stopHiringText = "Остановить найм";
        [SerializeField] private string untilGrenadiersText = "До гренадёров";
        [SerializeField] private string untilJaegersText = "До егерей";
        [SerializeField] private string untilCavalryText = "До отряда";
        [SerializeField] private string endlessText = "Без остановки";

        private Match match;
        private Building building;

        public void Initialize(Match match)
        {
            this.match = match;
            recruitButton.onClick.AddListener(OnToggleHiring);
            if (targetButton != null)
            {
                targetButton.onClick.AddListener(OnNextTarget);
            }

            SetBuilding(null);
        }

        public void SetBuilding(Building building)
        {
            this.building = building;
            panel.SetActive(building != null);
            Refresh();
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

            bool hires = building.Type != BuildingType.Headquarters;
            bool stable = building.Type == BuildingType.Stable;
            titleLabel.text = !hires ? headquartersTitle : stable ? stableTitle : barracksTitle;
            recruitButton.gameObject.SetActive(hires);
            recruitButton.interactable = BuildRules.CanHire(building);
            int cost = stable ? match.Settings.buildings.cavalryHireCost : match.Settings.buildings.hireCost;
            recruitLabel.text = building.IsHiring ? stopHiringText : string.Format(startHiringFormat, cost);
            if (targetButton != null)
            {
                targetButton.gameObject.SetActive(hires);
                targetLabel.text = GetTargetText(building.HireUntil);
            }

            statusLabel.text = GetStatus(hires);
        }

        private string GetTargetText(UnitType? target)
        {
            if (!target.HasValue)
            {
                return endlessText;
            }

            switch (target.Value)
            {
                case UnitType.Jaegers:
                    return untilJaegersText;
                case UnitType.Cavalry:
                    return untilCavalryText;
                default:
                    return untilGrenadiersText;
            }
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
                case HireWait.SquadReady:
                    return squadReadyStatus;
                case HireWait.Full:
                    return fullStatus;
                case HireWait.NoGold:
                    return noGoldStatus;
                default:
                    return hiringStatus;
            }
        }

        private void OnToggleHiring()
        {
            if (building != null)
            {
                match.Enqueue(new SetHiringCommand(building.Id, !building.IsHiring));
            }
        }

        /// <summary>Казарма: до гренадёров → до егерей → без остановки; конюшня: до отряда → без остановки.</summary>
        private void OnNextTarget()
        {
            if (building == null)
            {
                return;
            }

            UnitType? next;
            if (building.Type == BuildingType.Stable)
            {
                next = building.HireUntil.HasValue ? (UnitType?)null : UnitType.Cavalry;
            }
            else if (!building.HireUntil.HasValue)
            {
                next = UnitType.Grenadiers;
            }
            else
            {
                next = building.HireUntil.Value == UnitType.Grenadiers ? UnitType.Jaegers : (UnitType?)null;
            }

            match.Enqueue(new SetHireTargetCommand(building.Id, next));
        }
    }
}
