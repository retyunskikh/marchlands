using UnityEngine;

namespace Marchlands.UI
{
    /// <summary>
    /// Отодвигает элемент интерфейса от края экрана, к которому он прижат: на отступ безопасной зоны (вырез камеры)
    /// и ещё на cornerPadding — скруглённые углы экрана в безопасную зону не входят. Сдвиг считается от исходной
    /// позиции и обновляется при повороте экрана.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaInset : MonoBehaviour
    {
        [Tooltip("Запас от края по горизонтали и вертикали, в единицах холста (1920×1080).")]
        [SerializeField] private Vector2 cornerPadding = new Vector2(36f, 0f);

        private RectTransform rectTransform;
        private Canvas canvas;
        private Vector2 basePosition;
        private Rect appliedSafeArea;
        private Vector2Int appliedScreen;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>();
            basePosition = rectTransform.anchoredPosition;
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != appliedSafeArea || Screen.width != appliedScreen.x || Screen.height != appliedScreen.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            appliedSafeArea = Screen.safeArea;
            appliedScreen = new Vector2Int(Screen.width, Screen.height);
            float scale = canvas != null && canvas.rootCanvas.scaleFactor > 0f ? canvas.rootCanvas.scaleFactor : 1f;
            Rect safe = Screen.safeArea;
            float left = safe.xMin / scale;
            float right = (Screen.width - safe.xMax) / scale;
            float bottom = safe.yMin / scale;
            float top = (Screen.height - safe.yMax) / scale;

            Vector2 anchor = (rectTransform.anchorMin + rectTransform.anchorMax) * 0.5f;
            Vector2 offset = Vector2.zero;
            if (anchor.x < 0.5f)
            {
                offset.x = left + cornerPadding.x;
            }
            else if (anchor.x > 0.5f)
            {
                offset.x = -(right + cornerPadding.x);
            }

            if (anchor.y < 0.5f)
            {
                offset.y = bottom + cornerPadding.y;
            }
            else if (anchor.y > 0.5f)
            {
                offset.y = -(top + cornerPadding.y);
            }

            rectTransform.anchoredPosition = basePosition + offset;
        }
    }
}
