using Marchlands.View;
using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>Кнопка «Музыка: вкл/выкл». Выбор запоминается между запусками.</summary>
    public sealed class MusicToggleButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text label;
        [SerializeField] private string onText = "Музыка: вкл";
        [SerializeField] private string offText = "Музыка: выкл";

        private void Awake()
        {
            button.onClick.AddListener(Toggle);
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Toggle()
        {
            MusicDirector.Enabled = !MusicDirector.Enabled;
            Refresh();
        }

        private void Refresh()
        {
            label.text = MusicDirector.Enabled ? onText : offText;
        }
    }
}
