using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Кружки слева вверху: гренадёр, фузилёр и (если есть конюшня) всадник, под каждым — счётчик людей в резерве
    /// («30/48»). Пока людей не хватает на отряд, иконка тусклая; хватает — яркая, нажатие строит отряд (с горном).
    /// </summary>
    public sealed class FormSquadPanel : MonoBehaviour
    {
        [FormerlySerializedAs("jaegersButton")]
        [SerializeField] private Button fusiliersButton;
        [SerializeField] private Button grenadiersButton;
        [SerializeField] private Button cavalryButton;
        [SerializeField] private Button artilleryButton;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip formClip;
        [SerializeField] private float formVolume = 0.9f;
        [Tooltip("{0} — название, {1} — людей в резерве, {2} — нужно на отряд.")]
        [SerializeField] private string countFormat = "{1}/{2}";
        [SerializeField] private string grenadiersName = "Гренадёры";
        [SerializeField] private string fusiliersName = "Фузилёры";
        [SerializeField] private string cavalryName = "Конница";
        [SerializeField] private string artilleryName = "Пушки";
        [SerializeField] private Color readyTint = Color.white;
        [SerializeField] private Color waitingTint = new Color(1f, 1f, 1f, 0.4f);

        private Match match;
        private Side playerSide;

        public void Initialize(Match match, Side playerSide)
        {
            this.match = match;
            this.playerSide = playerSide;
            fusiliersButton.onClick.AddListener(() => Form(UnitType.Fusiliers));
            grenadiersButton.onClick.AddListener(() => Form(UnitType.Grenadiers));
            if (cavalryButton != null)
            {
                cavalryButton.onClick.AddListener(() => Form(UnitType.Cavalry));
            }

            if (artilleryButton != null)
            {
                artilleryButton.onClick.AddListener(() => Form(UnitType.Artillery));
            }

            // Без штаба (песочницы) нанимать негде — счётчики не нужны.
            gameObject.SetActive(match.GetHeadquarters(playerSide) != null);
        }

        private void Update()
        {
            if (match == null)
            {
                return;
            }

            Refresh(grenadiersButton, UnitType.Grenadiers, grenadiersName, true);
            Refresh(fusiliersButton, UnitType.Fusiliers, fusiliersName, true);
            if (cavalryButton != null)
            {
                Refresh(cavalryButton, UnitType.Cavalry, cavalryName, Has(BuildingType.Stable));
            }

            if (artilleryButton != null)
            {
                Refresh(artilleryButton, UnitType.Artillery, artilleryName, Has(BuildingType.ArtilleryDepot));
            }
        }

        private void Refresh(Button button, UnitType type, string unitName, bool visible)
        {
            if (button.gameObject.activeSelf != visible)
            {
                button.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            bool ready = BuildRules.CanFormSquad(match, playerSide, type);
            button.interactable = ready;
            Color tint = ready ? readyTint : waitingTint;
            foreach (Graphic graphic in button.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.name == "Icon")
                {
                    graphic.color = tint;
                }
                else if (graphic is Text label)
                {
                    label.text = string.Format(countFormat, unitName, match.GetPlayer(playerSide).GetReserve(type), match.Settings.Unit(type).squadSize);
                }
            }
        }

        private bool Has(BuildingType type)
        {
            foreach (Building building in match.Buildings)
            {
                if (building.Owner == playerSide && building.Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private void Form(UnitType type)
        {
            if (!BuildRules.CanFormSquad(match, playerSide, type))
            {
                return;
            }

            match.Enqueue(new FormSquadCommand(playerSide, type));
            if (audioSource != null && formClip != null)
            {
                audioSource.PlayOneShot(formClip, formVolume);
            }
        }
    }
}
