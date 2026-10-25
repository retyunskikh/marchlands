using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Кнопка перестроения выделенного отряда. Видна только при выделении, неактивна во время перестроения.
    /// Временная: на этапе 2 переедет в нижнюю панель приказов.
    /// </summary>
    public sealed class FormationToggleButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text label;
        [SerializeField] private string toColumnText = "В колонну";
        [SerializeField] private string toLineText = "В линию";
        [SerializeField] private string changingText = "Перестроение…";

        private Match match;
        private Squad squad;

        public void Initialize(Match match)
        {
            this.match = match;
            button.onClick.AddListener(OnClick);
            SetSquad(null);
        }

        public void SetSquad(Squad squad)
        {
            this.squad = squad;
            button.gameObject.SetActive(squad != null);
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (squad == null)
            {
                return;
            }

            button.interactable = !squad.IsChangingFormation;
            label.text = squad.IsChangingFormation
                ? changingText
                : squad.Formation == Formation.Line ? toColumnText : toLineText;
        }

        private void OnClick()
        {
            if (squad == null || squad.IsChangingFormation)
            {
                return;
            }

            Formation next = squad.Formation == Formation.Line ? Formation.Column : Formation.Line;
            match.Enqueue(new ChangeFormationCommand(squad.Id, next));
        }
    }
}
