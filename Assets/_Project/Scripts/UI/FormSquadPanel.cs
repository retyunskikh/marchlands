using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Иконки слева вверху: «Егеря» и «Гренадёры». Иконка появляется, когда у казарм ждёт достаточно людей
    /// на такой отряд; нажатие строит отряд у казармы. Появление и нажатие озвучены.
    /// </summary>
    public sealed class FormSquadPanel : MonoBehaviour
    {
        [SerializeField] private Button jaegersButton;
        [SerializeField] private Button grenadiersButton;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip appearClip;
        [SerializeField] private AudioClip formClip;
        [SerializeField] private float appearVolume = 0.7f;
        [SerializeField] private float formVolume = 0.9f;

        private Match match;
        private Side playerSide;
        private bool jaegersShown;
        private bool grenadiersShown;

        public void Initialize(Match match, Side playerSide)
        {
            this.match = match;
            this.playerSide = playerSide;
            jaegersButton.onClick.AddListener(() => Form(UnitType.Jaegers));
            grenadiersButton.onClick.AddListener(() => Form(UnitType.Grenadiers));
            jaegersShown = SetVisible(jaegersButton, false, true);
            grenadiersShown = SetVisible(grenadiersButton, false, true);
        }

        private void Update()
        {
            if (match == null)
            {
                return;
            }

            bool appeared = false;
            jaegersShown = Refresh(jaegersButton, UnitType.Jaegers, jaegersShown, ref appeared);
            grenadiersShown = Refresh(grenadiersButton, UnitType.Grenadiers, grenadiersShown, ref appeared);
            if (appeared)
            {
                Play(appearClip, appearVolume);
            }
        }

        private bool Refresh(Button button, UnitType type, bool shown, ref bool appeared)
        {
            bool available = BuildRules.CanFormSquad(match, playerSide, type);
            if (available && !shown)
            {
                appeared = true;
            }

            return SetVisible(button, available, false);
        }

        private static bool SetVisible(Button button, bool visible, bool force)
        {
            if (force || button.gameObject.activeSelf != visible)
            {
                button.gameObject.SetActive(visible);
            }

            return visible;
        }

        private void Form(UnitType type)
        {
            if (!BuildRules.CanFormSquad(match, playerSide, type))
            {
                return;
            }

            match.Enqueue(new FormSquadCommand(playerSide, type));
            Play(formClip, formVolume);
        }

        private void Play(AudioClip clip, float volume)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }
    }
}
