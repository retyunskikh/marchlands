using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>Иконка найма рода войск в карточке здания: портрет, подпись, подсветка «найм идёт» и полоска до рекрута.</summary>
    public sealed class HireToggle : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image frame;
        [SerializeField] private Text label;
        [SerializeField] private Image progressFill;
        [SerializeField] private Button infoButton;
        [SerializeField] private Color onColor = new Color(1f, 0.85f, 0.35f);
        [SerializeField] private Color offColor = new Color(0.95f, 0.92f, 0.82f);

        public Button Button => button;

        public Button InfoButton => infoButton;

        public void Show(string text, bool hiring, float progress)
        {
            label.text = text;
            frame.color = hiring ? onColor : offColor;
            if (progressFill != null)
            {
                progressFill.enabled = hiring;
                progressFill.fillAmount = Mathf.Clamp01(progress);
            }
        }
    }
}
