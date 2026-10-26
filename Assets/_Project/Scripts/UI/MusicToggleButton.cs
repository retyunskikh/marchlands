using Marchlands.View;
using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Кнопка музыки: иконкой (нота / перечёркнутая нота) или надписью «Музыка: вкл/выкл», если иконки нет.
    /// Выбор запоминается между запусками.
    /// </summary>
    public sealed class MusicToggleButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text label;
        [SerializeField] private string onText = "Музыка: вкл";
        [SerializeField] private string offText = "Музыка: выкл";
        [Tooltip("Если задана иконка, надпись не показывается.")]
        [SerializeField] private Image icon;
        [SerializeField] private Sprite onSprite;
        [SerializeField] private Sprite offSprite;

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
            bool enabledNow = MusicDirector.Enabled;
            bool useIcon = icon != null && onSprite != null && offSprite != null;
            if (useIcon)
            {
                icon.sprite = enabledNow ? onSprite : offSprite;
            }

            if (label != null)
            {
                label.gameObject.SetActive(!useIcon);
                label.text = enabledNow ? onText : offText;
            }
        }
    }
}
