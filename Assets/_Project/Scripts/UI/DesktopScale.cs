using Marchlands.View;
using UnityEngine;

namespace Marchlands.UI
{
    /// <summary>
    /// На ПК элемент интерфейса компактнее: масштаб вокруг заданной точки (например, правого нижнего угла для
    /// кнопок приказов). Под палец кнопки крупные, под мышь — нет. На телефоне ничего не меняет.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class DesktopScale : MonoBehaviour
    {
        [Tooltip("Масштаб на ПК.")]
        [SerializeField] private float scale = 0.72f;
        [Tooltip("Вокруг какой точки сжимать (0..1 по ширине и высоте элемента), например (1, 0) — правый нижний угол.")]
        [SerializeField] private Vector2 pivot = new Vector2(0.5f, 0f);

        private void Awake()
        {
            if (!Platform.IsDesktop)
            {
                return;
            }

            var rect = (RectTransform)transform;
            // У растянутого элемента без отступов смена опорной точки его не сдвигает.
            Vector2 offsetMin = rect.offsetMin;
            Vector2 offsetMax = rect.offsetMax;
            Vector2 size = rect.rect.size;
            Vector2 shift = Vector2.Scale(pivot - rect.pivot, size);
            rect.pivot = pivot;
            rect.anchoredPosition += shift;
            if (rect.anchorMin != rect.anchorMax)
            {
                rect.offsetMin = offsetMin;
                rect.offsetMax = offsetMax;
            }

            rect.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
