using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Всплывающая подсказка рядом с курсором: появляется через <see cref="Delay"/> секунды наведения, не уходит за
    /// край экрана. Создаётся сама на холсте той кнопки, на которую навели (поверх всего на нём).
    /// </summary>
    public sealed class TooltipView : MonoBehaviour
    {
        private const float Delay = 0.35f;
        private const float MaxWidth = 620f;
        private static readonly Vector2 Offset = new Vector2(22f, -26f);
        private static TooltipView instance;

        private RectTransform canvasRect;
        private RectTransform panel;
        private Text label;
        private HoverTip owner;
        private Vector2 pointer;
        private float shownAt;

        public static void Show(HoverTip tip, Vector2 screenPosition)
        {
            Canvas canvas = tip.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            canvas = canvas.rootCanvas;
            if (instance == null || instance.canvasRect != canvas.transform)
            {
                if (instance != null)
                {
                    Destroy(instance.gameObject);
                }

                instance = Create(canvas, tip);
            }

            instance.owner = tip;
            instance.pointer = screenPosition;
            instance.shownAt = Time.unscaledTime + Delay;
            instance.label.text = tip.Text;
            instance.panel.gameObject.SetActive(false);
        }

        public static void Move(HoverTip tip, Vector2 screenPosition)
        {
            if (instance != null && instance.owner == tip)
            {
                instance.pointer = screenPosition;
            }
        }

        public static void Hide(HoverTip tip)
        {
            if (instance != null && instance.owner == tip)
            {
                instance.owner = null;
                instance.panel.gameObject.SetActive(false);
            }
        }

        private static TooltipView Create(Canvas canvas, HoverTip tip)
        {
            var root = new GameObject("Подсказка", typeof(RectTransform), typeof(Canvas));
            var rootRect = (RectTransform)root.transform;
            rootRect.SetParent(canvas.transform, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;
            var rootCanvas = root.GetComponent<Canvas>();
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = 500;

            var panelObject = new GameObject("Панель", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var panelRect = (RectTransform)panelObject.transform;
            panelRect.SetParent(rootRect, false);
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.zero;
            var background = panelObject.GetComponent<Image>();
            background.color = new Color(0.1f, 0.08f, 0.06f, 0.94f);
            background.raycastTarget = false;
            var layout = panelObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 14, 16);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            var fitter = panelObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textObject = new GameObject("Текст", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            textObject.transform.SetParent(panelRect, false);
            var text = textObject.GetComponent<Text>();
            Text sample = canvas.GetComponentInChildren<Text>(true);
            text.font = sample != null ? sample.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 28;
            text.color = new Color(0.97f, 0.93f, 0.84f);
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            textObject.GetComponent<LayoutElement>().preferredWidth = MaxWidth;

            var view = root.AddComponent<TooltipView>();
            view.canvasRect = (RectTransform)canvas.transform;
            view.panel = panelRect;
            view.label = text;
            panelObject.SetActive(false);
            return view;
        }

        private void LateUpdate()
        {
            if (owner == null || !owner.isActiveAndEnabled)
            {
                panel.gameObject.SetActive(false);
                return;
            }

            if (Time.unscaledTime < shownAt)
            {
                return;
            }

            if (!panel.gameObject.activeSelf)
            {
                panel.gameObject.SetActive(true);
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            }

            // Рядом с курсором, справа снизу; у правого или нижнего края — по другую сторону курсора.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, pointer, null, out Vector2 local);
            Vector2 size = panel.rect.size;
            Vector2 half = canvasRect.rect.size * 0.5f;
            Vector2 position = local + Offset;
            float x = position.x + size.x > half.x ? local.x - Offset.x - size.x : position.x;
            float y = position.y - size.y < -half.y ? local.y - Offset.y + size.y : position.y;
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(x, y) + half;
        }
    }
}
