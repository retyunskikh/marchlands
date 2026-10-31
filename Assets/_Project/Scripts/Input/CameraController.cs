using UnityEngine;

namespace Marchlands.Input
{
    /// <summary>
    /// Ортографическая камера вида сверху: сдвиг и масштаб относительно точки на экране.
    /// Камера не уходит за границы карты дальше небольшого запаса и не отдаляется настолько,
    /// чтобы за краем карты было пусто.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraController : MonoBehaviour
    {
        [Tooltip("Центр карты в мире.")]
        [SerializeField] private Vector2 mapCenter = Vector2.zero;
        [Tooltip("Размер карты, u (как у фона).")]
        [SerializeField] private Vector2 mapSize = new Vector2(120f, 80f);
        [Tooltip("Насколько можно заглянуть за край карты, u.")]
        [SerializeField] private float boundsMargin = 3f;

        private Camera targetCamera;
        private InputConfig config;

        /// <summary>Границы карты из сценария: центр и размер области, за которую камера не заглядывает дальше запаса.</summary>
        public void SetMapBounds(Vector2 center, Vector2 size)
        {
            mapCenter = center;
            mapSize = size;
            if (targetCamera != null)
            {
                targetCamera.orthographicSize = ClampZoom(targetCamera.orthographicSize);
                ClampToMap();
            }
        }

        public void Initialize(InputConfig config)
        {
            this.config = config;
            targetCamera = GetComponent<Camera>();
            targetCamera.orthographicSize = ClampZoom(targetCamera.orthographicSize);
            ClampToMap();
        }

        public Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            return targetCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 0f));
        }

        public float PixelsToWorld(float pixels)
        {
            return pixels * 2f * targetCamera.orthographicSize / Screen.height;
        }

        /// <summary>Сдвигает камеру так, чтобы точка под пальцем осталась под пальцем.</summary>
        public void Pan(Vector2 screenDelta)
        {
            Vector2 worldDelta = screenDelta * PixelsToWorld(1f);
            transform.position -= new Vector3(worldDelta.x, worldDelta.y, 0f);
            ClampToMap();
        }

        /// <param name="spread">Больше 1 — пальцы разошлись, камера приближается.</param>
        public void Zoom(Vector2 pivotScreen, float spread)
        {
            Vector2 before = ScreenToWorld(pivotScreen);
            targetCamera.orthographicSize = ClampZoom(targetCamera.orthographicSize / spread);
            Vector2 after = ScreenToWorld(pivotScreen);
            transform.position += new Vector3(before.x - after.x, before.y - after.y, 0f);
            ClampToMap();
        }

        /// <summary>
        /// zoomMin–zoomMax из конфига, но видимая область не больше карты (с запасом) ни по одной оси —
        /// на широком экране это меньше zoomMax.
        /// </summary>
        private float ClampZoom(float size)
        {
            Vector2 allowedHalf = mapSize * 0.5f + Vector2.one * boundsMargin;
            float fitsMap = Mathf.Min(allowedHalf.y, allowedHalf.x / targetCamera.aspect);
            float max = Mathf.Max(config.ZoomMin, Mathf.Min(config.ZoomMax, fitsMap));
            return Mathf.Clamp(size, config.ZoomMin, max);
        }

        /// <summary>
        /// Держит видимую область внутри карты с запасом boundsMargin. Если карта по какой-то оси меньше
        /// экрана, камера по этой оси встаёт по центру карты.
        /// </summary>
        private void ClampToMap()
        {
            float halfHeight = targetCamera.orthographicSize;
            float halfWidth = halfHeight * targetCamera.aspect;
            Vector2 allowedHalf = mapSize * 0.5f + Vector2.one * boundsMargin;

            Vector3 position = transform.position;
            position.x = ClampAxis(position.x, mapCenter.x, allowedHalf.x, halfWidth);
            position.y = ClampAxis(position.y, mapCenter.y, allowedHalf.y, halfHeight);
            transform.position = position;
        }

        private static float ClampAxis(float value, float center, float allowedHalf, float viewHalf)
        {
            float freedom = allowedHalf - viewHalf;
            return freedom <= 0f ? center : Mathf.Clamp(value, center - freedom, center + freedom);
        }
    }
}
