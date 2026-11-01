using System;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Режим постановки здания: подсказка, «Поставить» и «Отмена». Постройку начинает карточка штаба (<see cref="BuildingPanel"/>).
    /// Сама команда отдаётся вводом, который знает выбранное место. Без штаба панель скрыта.
    /// </summary>
    public sealed class BuildPanel : MonoBehaviour
    {
        [SerializeField] private GameObject placementButtons;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Text hintLabel;
        [SerializeField] private string chooseSiteText = "Тапните, где поставить здание";
        [SerializeField] private string validSiteText = "Место подходит";
        [Header("Почему здесь нельзя")]
        [SerializeField] private string farFromHeadquartersText = "Слишком далеко от штаба — ближе к нему";
        [SerializeField] private string tooCloseToBuildingText = "Вплотную к другому зданию — отступите от него";
        [SerializeField] private string tooCloseToVillageText = "Вплотную к деревне — отступите от неё";
        [SerializeField] private string noGoldText = "Не хватает золота";
        [SerializeField] private string atLimitText = "Такое здание уже есть";

        private Match match;
        private Side playerSide;
        private bool isPlacing;
        private bool hasSite;
        private bool canConfirm;
        private SiteProblem problem;

        public event Action ConfirmRequested;

        public event Action CancelRequested;

        public void Initialize(Match match, Side playerSide)
        {
            this.match = match;
            this.playerSide = playerSide;
            confirmButton.onClick.AddListener(() => ConfirmRequested?.Invoke());
            cancelButton.onClick.AddListener(() => CancelRequested?.Invoke());
            gameObject.SetActive(match.GetHeadquarters(playerSide) != null);
            SetPlacementState(false, false, SiteProblem.None);
        }

        /// <param name="placing">Идёт выбор места.</param>
        /// <param name="siteChosen">Место уже выбрано тапом.</param>
        /// <param name="siteProblem">Почему на выбранном месте строить нельзя; None — можно.</param>
        public void SetPlacementState(bool placing, bool siteChosen, SiteProblem siteProblem)
        {
            isPlacing = placing;
            hasSite = siteChosen;
            problem = siteProblem;
            canConfirm = placing && siteChosen && siteProblem == SiteProblem.None;
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
            placementButtons.SetActive(isPlacing);
            confirmButton.interactable = canConfirm;
            hintLabel.text = !hasSite ? chooseSiteText : canConfirm ? validSiteText : ProblemText(problem);
        }

        private string ProblemText(SiteProblem siteProblem)
        {
            switch (siteProblem)
            {
                case SiteProblem.FarFromHeadquarters:
                    return farFromHeadquartersText;
                case SiteProblem.TooCloseToBuilding:
                    return tooCloseToBuildingText;
                case SiteProblem.TooCloseToVillage:
                    return tooCloseToVillageText;
                case SiteProblem.NoGold:
                    return noGoldText;
                case SiteProblem.AtLimit:
                    return atLimitText;
                default:
                    return farFromHeadquartersText;
            }
        }
    }
}
