using System.Globalization;
using Marchlands.Simulation.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Верхняя панель: золото, число деревень и резерв игрока, а пока кто-то держит ⅔ деревень — таймер
    /// доминирования (раздел 4 docs/design.md).
    /// </summary>
    public sealed class TopBar : MonoBehaviour
    {
        [SerializeField] private Text goldLabel;
        [SerializeField] private Text villagesLabel;
        [SerializeField] private Text reserveLabel;
        [SerializeField] private Text dominationLabel;
        [Tooltip("{0} — золото.")]
        [SerializeField] private string goldFormat = "Золото: {0}";
        [Tooltip("{0} — свои деревни, {1} — всего.")]
        [SerializeField] private string villagesFormat = "Деревни: {0} / {1}";
        [Tooltip("{0} — солдат в резерве.")]
        [SerializeField] private string reserveFormat = "Резерв: {0}";
        [Tooltip("{0} — сторона, {1} — сколько осталось до победы (м:сс).")]
        [SerializeField] private string dominationFormat = "{0} держит деревни: {1}";
        [SerializeField] private string franceName = "Франция";
        [SerializeField] private string austriaName = "Австрия";
        [SerializeField] private Color ownDominationColor = new Color(0.6f, 1f, 0.6f);
        [SerializeField] private Color enemyDominationColor = new Color(1f, 0.55f, 0.45f);

        // Разряды отделяются пробелом: 1 250.
        private static readonly NumberFormatInfo GroupedNumbers = new NumberFormatInfo { NumberGroupSeparator = " " };

        private Match match;
        private Side playerSide;
        private int shownGold = -1;
        private int shownVillages = -1;
        private int shownReserve = -1;
        private int shownDominationSeconds = -1;

        public void Initialize(Match match, Side playerSide)
        {
            this.match = match;
            this.playerSide = playerSide;
            villagesLabel.gameObject.SetActive(match.Villages.Count > 0);
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
            int gold = match.GetPlayer(playerSide).Gold;
            if (gold != shownGold)
            {
                shownGold = gold;
                goldLabel.text = string.Format(goldFormat, gold.ToString("#,0", GroupedNumbers));
            }

            int villages = match.CountVillages(playerSide);
            if (villages != shownVillages)
            {
                shownVillages = villages;
                villagesLabel.text = string.Format(villagesFormat, villages, match.Villages.Count);
            }

            int reserve = match.GetPlayer(playerSide).Reserve;
            if (reserve != shownReserve)
            {
                shownReserve = reserve;
                reserveLabel.text = string.Format(reserveFormat, reserve.ToString("#,0", GroupedNumbers));
            }

            RefreshDomination();
        }

        private void RefreshDomination()
        {
            Side leader = match.GetDominationTime(Side.France) >= match.GetDominationTime(Side.Austria) ? Side.France : Side.Austria;
            float held = match.GetDominationTime(leader);
            bool visible = match.VictoryEnabled && held > 0f;
            dominationLabel.gameObject.SetActive(visible);
            if (!visible)
            {
                shownDominationSeconds = -1;
                return;
            }

            int remaining = Mathf.CeilToInt(Mathf.Max(0f, match.Settings.victory.dominationTime - held));
            if (remaining == shownDominationSeconds)
            {
                return;
            }

            shownDominationSeconds = remaining;
            string sideName = leader == Side.France ? franceName : austriaName;
            dominationLabel.text = string.Format(dominationFormat, sideName, $"{remaining / 60}:{remaining % 60:00}");
            dominationLabel.color = leader == playerSide ? ownDominationColor : enemyDominationColor;
        }
    }
}
