using System;
using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Панель подсказки обучения вверху экрана: текст урока, номер шага, «Далее» (у поясняющих шагов)
    /// и «Пропустить обучение».
    /// </summary>
    public sealed class TutorialPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text messageLabel;
        [SerializeField] private Text progressLabel;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private string progressFormat = "Обучение · {0} из {1}";

        public event Action NextPressed;

        public event Action SkipPressed;

        private void Awake()
        {
            nextButton.onClick.AddListener(() => NextPressed?.Invoke());
            skipButton.onClick.AddListener(() => SkipPressed?.Invoke());
            Hide();
        }

        /// <param name="waitsForNext">Шаг-пояснение: дальше — по «Далее»; иначе — когда игрок выполнит задание.</param>
        public void Show(string message, bool waitsForNext, int index, int total)
        {
            panel.SetActive(true);
            messageLabel.text = message;
            progressLabel.text = string.Format(progressFormat, index + 1, total);
            nextButton.gameObject.SetActive(waitsForNext);
        }

        public void Hide()
        {
            panel.SetActive(false);
        }
    }
}
