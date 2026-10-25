using UnityEngine;

namespace Marchlands.Input
{
    /// <summary>Ортографическая камера вида сверху: сдвиг и масштаб относительно точки на экране.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraController : MonoBehaviour
    {
        private Camera targetCamera;
        private InputConfig config;

        public void Initialize(InputConfig config)
        {
            this.config = config;
            targetCamera = GetComponent<Camera>();
            targetCamera.orthographicSize = Mathf.Clamp(targetCamera.orthographicSize, config.ZoomMin, config.ZoomMax);
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
        }

        /// <param name="spread">Больше 1 — пальцы разошлись, камера приближается.</param>
        public void Zoom(Vector2 pivotScreen, float spread)
        {
            Vector2 before = ScreenToWorld(pivotScreen);
            targetCamera.orthographicSize = Mathf.Clamp(targetCamera.orthographicSize / spread, config.ZoomMin, config.ZoomMax);
            Vector2 after = ScreenToWorld(pivotScreen);
            transform.position += new Vector3(before.x - after.x, before.y - after.y, 0f);
        }
    }
}
