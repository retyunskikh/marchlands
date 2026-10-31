using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Приказы выделенной группы своей толпы рекрутов — только «Распустить» справа внизу, на месте кнопок приказов
    /// отряда. На кнопке — сколько людей уйдёт: все ждущие этого рода войск, и те, кто ещё идёт к толпе.
    /// </summary>
    public sealed class CampPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Button disbandButton;
        [SerializeField] private Text disbandLabel;
        [Tooltip("{0} — сколько людей будет распущено.")]
        [SerializeField] private string disbandFormat = "Распустить\n{0}";

        private Match match;
        private Side side;
        private UnitType? type;

        public void Initialize(Match match, Side side)
        {
            this.match = match;
            this.side = side;
            disbandButton.onClick.AddListener(OnDisband);
            SetCamp(null);
        }

        public void SetCamp(UnitType? type)
        {
            this.type = type;
            panel.SetActive(type.HasValue);
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (match == null || !type.HasValue)
            {
                return;
            }

            string text = string.Format(disbandFormat, match.GetPlayer(side).GetReserve(type.Value));
            if (disbandLabel.text != text)
            {
                disbandLabel.text = text;
            }
        }

        private void OnDisband()
        {
            if (type.HasValue)
            {
                match.Enqueue(new DisbandReserveCommand(side, type.Value));
            }
        }
    }
}
