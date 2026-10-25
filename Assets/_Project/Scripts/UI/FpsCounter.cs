using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>Счётчик кадров для замера производительности на телефоне. Виден только в development-сборке и редакторе.</summary>
    public sealed class FpsCounter : MonoBehaviour
    {
        [SerializeField] private Text label;
        [Tooltip("За сколько секунд усреднять, s.")]
        [SerializeField] private float window = 0.5f;
        [SerializeField] private string format = "FPS {0:0}  мин {1:0}";

        private float elapsed;
        private int frames;
        private float worstFrame;

        private void Awake()
        {
            gameObject.SetActive(Debug.isDebugBuild);
        }

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            elapsed += delta;
            frames++;
            worstFrame = Mathf.Max(worstFrame, delta);
            if (elapsed < window)
            {
                return;
            }

            label.text = string.Format(format, frames / elapsed, worstFrame > 0f ? 1f / worstFrame : 0f);
            elapsed = 0f;
            frames = 0;
            worstFrame = 0f;
        }
    }
}
