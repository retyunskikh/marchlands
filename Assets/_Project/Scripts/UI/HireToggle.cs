using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>Иконка найма рода войск или постройки в карточке здания: портрет, подпись, цена с монеткой, подсветка «найм идёт» и полоска до рекрута.</summary>
    public sealed class HireToggle : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image frame;
        [SerializeField] private Image icon;
        [SerializeField] private Text label;
        [Tooltip("Подпись над картинкой — у иконок постройки; у иконок найма не задана.")]
        [SerializeField] private Text nameLabel;
        [SerializeField] private Image progressFill;
        [SerializeField] private Button infoButton;
        [Tooltip("«Распустить» ждущих этого рода войск — у иконок найма; у иконок постройки не задана.")]
        [SerializeField] private Button disbandButton;
        [Tooltip("Надпись на «Распустить»: {0} — сколько людей уйдёт.")]
        [SerializeField] private string disbandFormat = "Распустить {0}";
        [SerializeField] private Color onColor = new Color(1f, 0.85f, 0.35f);
        [SerializeField] private Color offColor = new Color(0.95f, 0.92f, 0.82f);
        [SerializeField] private Color availableTint = Color.white;
        [SerializeField] private Color unavailableTint = new Color(1f, 1f, 1f, 0.4f);

        public Button Button => button;

        public Button InfoButton => infoButton;

        public Button DisbandButton => disbandButton;

        /// <summary>«Распустить» над иконкой видна, только когда ждут люди этого рода войск, и пишет, сколько их уйдёт.</summary>
        public void SetDisband(int waiting)
        {
            if (disbandButton == null)
            {
                return;
            }

            bool visible = waiting > 0;
            if (disbandButton.gameObject.activeSelf != visible)
            {
                disbandButton.gameObject.SetActive(visible);
            }

            Text text = disbandButton.GetComponentInChildren<Text>(true);
            string value = string.Format(disbandFormat, waiting);
            if (visible && text != null && text.text != value)
            {
                text.text = value;
            }
        }

        public void SetIcon(Sprite sprite)
        {
            if (icon != null && sprite != null && icon.sprite != sprite)
            {
                icon.sprite = sprite;
            }
        }

        public void SetName(string text)
        {
            if (nameLabel != null && nameLabel.text != text)
            {
                nameLabel.text = text;
            }
        }

        /// <summary>Бледная иконка — нажать нельзя (например, не хватает золота).</summary>
        public void SetAvailable(bool available)
        {
            Color tint = available ? availableTint : unavailableTint;
            if (icon != null)
            {
                icon.color = tint;
            }

            label.color = new Color(label.color.r, label.color.g, label.color.b, tint.a);
            if (nameLabel != null)
            {
                nameLabel.color = new Color(nameLabel.color.r, nameLabel.color.g, nameLabel.color.b, tint.a);
            }
        }

        /// <param name="progress">Доля до следующего рекрута этого рода войск; отрицательная — сейчас нанимают не его.</param>
        public void Show(string text, bool hiring, float progress)
        {
            label.text = text;
            frame.color = hiring ? onColor : offColor;
            if (progressFill != null)
            {
                progressFill.enabled = hiring && progress >= 0f;
                progressFill.fillAmount = Mathf.Clamp01(progress);
            }
        }
    }
}
