using System;
using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Кнопка необратимого действия (роспуск — люди уходят навсегда): первое нажатие только предупреждает
    /// («Точно? Уйдут навсегда»), второе в течение confirmTime выполняет. Не нажали — кнопка возвращается в обычный вид.
    /// </summary>
    public sealed class ConfirmButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text label;
        [SerializeField] private Image background;
        [Tooltip("Надпись после первого нажатия.")]
        [SerializeField] private string confirmText = "Точно?\nУйдут навсегда";
        [Tooltip("Сколько секунд ждать второго нажатия (не зависит от паузы и ускорения).")]
        [SerializeField] private float confirmTime = 3f;
        [SerializeField] private Color idleColor = new Color(0.72f, 0.17f, 0.13f);
        [SerializeField] private Color armedColor = new Color(0.92f, 0.22f, 0.1f);

        private string idleText = string.Empty;
        private bool armed;
        private float armedUntil;

        /// <summary>Второе нажатие — действие подтверждено.</summary>
        public event Action Confirmed;

        public bool IsArmed => armed;

        /// <summary>Спрашивать ли «Точно?»; false — действие сразу по первому нажатию (кнопка остаётся красной).</summary>
        public bool RequireConfirm { get; set; } = true;

        private void Awake()
        {
            button.onClick.AddListener(OnClick);
            Disarm();
        }

        private void OnDisable()
        {
            Disarm();
        }

        /// <summary>Обычная надпись (например, «Распустить 5 — навсегда»); пока ждём подтверждения, не меняется.</summary>
        public void SetText(string text)
        {
            idleText = text;
            if (!armed && label != null && label.text != text)
            {
                label.text = text;
            }
        }

        private void Update()
        {
            if (armed && Time.unscaledTime > armedUntil)
            {
                Disarm();
            }

            if (armed && background != null)
            {
                // Тревожное мерцание, пока ждём второго нажатия.
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);
                background.color = Color.Lerp(idleColor, armedColor, pulse);
            }
        }

        private void OnClick()
        {
            if (!armed && RequireConfirm)
            {
                armed = true;
                armedUntil = Time.unscaledTime + confirmTime;
                if (label != null)
                {
                    label.text = confirmText;
                }

                return;
            }

            Disarm();
            Confirmed?.Invoke();
        }

        private void Disarm()
        {
            armed = false;
            if (label != null)
            {
                label.text = idleText;
            }

            if (background != null)
            {
                background.color = idleColor;
            }
        }
    }
}
