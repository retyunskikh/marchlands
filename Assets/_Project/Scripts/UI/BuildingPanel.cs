using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Панель выделенного здания: название, состояние постройки или найма и у казармы — кнопка «Начать / Остановить найм».
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
        [SerializeField] private string idleStatus = "Найм остановлен";
        [SerializeField] private string hiringStatus = "Идёт найм";
        [SerializeField] private string noGoldStatus = "Найм: не хватает золота";
        [SerializeField] private string fullStatus = "Найм: у казарм нет места";
        [Tooltip("{0} — цена одного рекрута.")]
        [SerializeField] private string startHiringFormat = "Начать найм ({0} за чел.)";
        [SerializeField] private string stopHiringText = "Остановить найм";

        private Match match;
        private Building building;

        public void Initialize(Match match)
        {
            this.match = match;
            recruitButton.onClick.AddListener(OnToggleHiring);
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
            recruitButton.interactable = BuildRules.CanHire(building);
            recruitLabel.text = building.IsHiring
                ? stopHiringText
                : string.Format(startHiringFormat, match.Settings.buildings.hireCost);
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

            if (!building.IsHiring)
            {
                return idleStatus;
            }

            var player = match.GetPlayer(building.Owner);
            if (player.Reserve >= match.Settings.buildings.maxWaitingRecruits)
            {
                return fullStatus;
            }

            return player.Gold < match.Settings.buildings.hireCost ? noGoldStatus : hiringStatus;
        }

        private void OnToggleHiring()
        {
            if (building != null)
            {
                match.Enqueue(new SetHiringCommand(building.Id, !building.IsHiring));
            }
        }
    }
}
