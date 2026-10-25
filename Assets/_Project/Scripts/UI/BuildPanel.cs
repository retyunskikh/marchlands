using System;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Постройка зданий: кнопки «Казарма (цена)» и «Конюшня (цена)», а в режиме постановки — «Поставить» и «Отмена».
    /// Сама команда отдаётся вводом, который знает выбранное место. Без штаба панель скрыта.
    /// </summary>
    public sealed class BuildPanel : MonoBehaviour
    {
        [SerializeField] private Button buildButton;
        [SerializeField] private Text buildLabel;
        [Tooltip("{0} — цена казармы.")]
        [SerializeField] private string buildFormat = "Казарма ({0})";
        [SerializeField] private Button stableButton;
        [SerializeField] private Text stableLabel;
        [Tooltip("{0} — цена конюшни.")]
        [SerializeField] private string stableFormat = "Конюшня ({0})";
        [SerializeField] private GameObject placementButtons;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Text hintLabel;
        [SerializeField] private string chooseSiteText = "Тапните, где поставить здание";
        [SerializeField] private string validSiteText = "Место подходит";
        [SerializeField] private string invalidSiteText = "Здесь нельзя: далеко от штаба, тесно или мало золота";

        private Match match;
        private Side playerSide;
        private bool isPlacing;
        private bool hasSite;
        private bool canConfirm;

        /// <summary>Нажата кнопка постройки здания этого типа.</summary>
        public event Action<BuildingType> BuildRequested;

        public event Action ConfirmRequested;

        public event Action CancelRequested;

        public void Initialize(Match match, Side playerSide)
        {
            this.match = match;
            this.playerSide = playerSide;
            buildLabel.text = string.Format(buildFormat, match.Settings.buildings.barracksCost);
            buildButton.onClick.AddListener(() => BuildRequested?.Invoke(BuildingType.Barracks));
            if (stableButton != null)
            {
                stableLabel.text = string.Format(stableFormat, match.Settings.buildings.stableCost);
                stableButton.onClick.AddListener(() => BuildRequested?.Invoke(BuildingType.Stable));
            }
            confirmButton.onClick.AddListener(() => ConfirmRequested?.Invoke());
            cancelButton.onClick.AddListener(() => CancelRequested?.Invoke());
            gameObject.SetActive(match.GetHeadquarters(playerSide) != null);
            SetPlacementState(false, false, false);
        }

        /// <param name="placing">Идёт выбор места.</param>
        /// <param name="siteChosen">Место уже выбрано тапом.</param>
        /// <param name="confirmable">Выбранное место подходит и золота хватает.</param>
        public void SetPlacementState(bool placing, bool siteChosen, bool confirmable)
        {
            isPlacing = placing;
            hasSite = siteChosen;
            canConfirm = confirmable;
            Refresh();
        }

        private void Update()
        {
            if (match != null)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            buildButton.gameObject.SetActive(!isPlacing);
            buildButton.interactable = BuildRules.CanAffordBarracks(match, playerSide);
            if (stableButton != null)
            {
                stableButton.gameObject.SetActive(!isPlacing);
                stableButton.interactable = BuildRules.CanAfford(match, playerSide, BuildingType.Stable);
            }
            placementButtons.SetActive(isPlacing);
            confirmButton.interactable = canConfirm;
            hintLabel.text = !hasSite ? chooseSiteText : canConfirm ? validSiteText : invalidSiteText;
        }
    }
}
