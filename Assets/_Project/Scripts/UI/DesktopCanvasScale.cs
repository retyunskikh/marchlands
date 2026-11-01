using Marchlands.View;
using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Масштаб всего интерфейса на ПК. Раскладка рассчитана на телефон (1920×1080, кнопки под палец); на мониторе
    /// она мельче на <see cref="desktopScale"/> и подстраивается только под высоту экрана — сверхширокий монитор
    /// (например, 3440×1440) не раздувает её по ширине. На телефоне ничего не меняет.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class DesktopCanvasScale : MonoBehaviour
    {
        [Tooltip("Во сколько раз интерфейс на ПК мельче, чем на телефоне при той же высоте экрана.")]
        [SerializeField] private float desktopScale = 0.7f;

        private void Awake()
        {
            if (!Platform.IsDesktop)
            {
                return;
            }

            var scaler = GetComponent<CanvasScaler>();
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize || desktopScale <= 0f)
            {
                return;
            }

            scaler.referenceResolution /= desktopScale;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
        }
    }
}
