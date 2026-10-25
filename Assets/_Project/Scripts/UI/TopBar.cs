using System.Globalization;
using Marchlands.Simulation.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>Верхняя панель: золото и число деревень игрока (раздел 4 docs/design.md).</summary>
    public sealed class TopBar : MonoBehaviour
    {
        [SerializeField] private Text goldLabel;
        [SerializeField] private Text villagesLabel;
        [Tooltip("{0} — золото.")]
        [SerializeField] private string goldFormat = "Золото: {0}";
        [Tooltip("{0} — свои деревни, {1} — всего.")]
        [SerializeField] private string villagesFormat = "Деревни: {0} / {1}";

        // Разряды отделяются пробелом: 1 250.
        private static readonly NumberFormatInfo GroupedNumbers = new NumberFormatInfo { NumberGroupSeparator = " " };

        private Match match;
        private Side playerSide;
        private int shownGold = -1;
        private int shownVillages = -1;

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
        }
    }
}
