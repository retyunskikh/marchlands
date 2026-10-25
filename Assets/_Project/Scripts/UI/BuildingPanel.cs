using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Панель выделенного здания: название, состояние постройки или найма и кнопка «Нанять (цена)» у казармы.
    /// Видна только при выделении здания.
    /// </summary>
    public sealed class BuildingPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Button recruitButton;
        [SerializeField] private Text recruitLabel;
        [SerializeField] private string headquartersTitle = "Штаб";
        [SerializeField] private string barracksTitle = "Казарма";
        [SerializeField] private string headquartersStatus = "Здесь не нанимают";
        [Tooltip("{0} — процент постройки.")]
        [SerializeField] private string constructionFormat = "Строится… {0}%";
        [SerializeField] private string idleStatus = "Готова к найму";
        [Tooltip("{0} — в очереди, {1} — максимум, {2} — процент текущего найма.")]
        [SerializeField] private string recruitingFormat = "Очередь {0} / {1}, найм {2}%";
        [Tooltip("{0} — цена отряда.")]
        [SerializeField] private string recruitFormat = "Нанять ({0})";

        private Match match;
        private Building building;

        public void Initialize(Match match)
        {
            this.match = match;
            recruitLabel.text = string.Format(recruitFormat, match.Settings.buildings.squadCost);
            recruitButton.onClick.AddListener(OnRecruit);
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

            bool isBarracks = building.Type == BuildingType.Barracks;
            titleLabel.text = isBarracks ? barracksTitle : headquartersTitle;
            recruitButton.gameObject.SetActive(isBarracks);
            recruitButton.interactable = BuildRules.CanRecruit(match, building);
            statusLabel.text = GetStatus(isBarracks);
        }

        private string GetStatus(bool isBarracks)
        {
            if (!isBarracks)
            {
                return headquartersStatus;
            }

            if (!building.IsBuilt)
            {
                return string.Format(constructionFormat, Mathf.FloorToInt(building.BuildProgress * 100f));
            }

            if (building.RecruitQueue == 0)
            {
                return idleStatus;
            }

            float recruitTime = match.Settings.buildings.recruitTime;
            int percent = Mathf.FloorToInt((1f - building.RecruitRemaining / recruitTime) * 100f);
            return string.Format(recruitingFormat, building.RecruitQueue, match.Settings.buildings.recruitQueueMax, percent);
        }

        private void OnRecruit()
        {
            if (building != null)
            {
                match.Enqueue(new RecruitCommand(building.Id));
            }
        }
    }
}
