using System;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>Внешний вид зданий-заглушек и предпросмотра постройки. Цвета сторон — из стиля отрядов.</summary>
    [Serializable]
    public sealed class BuildingViewStyle
    {
        [SerializeField] private Sprite squareSprite;
        [SerializeField] private Sprite circleSprite;
        [SerializeField] private Vector2 headquartersSize = new Vector2(3.2f, 3.2f);
        [SerializeField] private Vector2 barracksSize = new Vector2(3.4f, 2.2f);
        [Tooltip("Доля тёмной внутренней части штаба.")]
        [SerializeField] private float headquartersInnerScale = 0.55f;
        [SerializeField] private Color innerColor = new Color(0.15f, 0.15f, 0.18f);
        [Tooltip("Прозрачность строящегося здания.")]
        [SerializeField] private float constructionAlpha = 0.45f;
        [SerializeField] private Vector2 progressBarSize = new Vector2(2.6f, 0.22f);
        [Tooltip("Насколько выше центра здания стоит полоска постройки или найма, u.")]
        [SerializeField] private float progressBarOffset = 2f;
        [SerializeField] private Color progressBackground = new Color(0f, 0f, 0f, 0.45f);
        [SerializeField] private Color progressColor = new Color(1f, 0.85f, 0.45f);
        [SerializeField] private Vector2 queueDotSize = new Vector2(0.3f, 0.3f);
        [SerializeField] private float queueDotSpacing = 0.45f;
        [SerializeField] private Color selectionColor = new Color(1f, 0.9f, 0.3f, 0.35f);
        [SerializeField] private float selectionPadding = 0.4f;

        [Header("Предпросмотр постройки")]
        [SerializeField] private Color buildRadiusColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color validSiteColor = new Color(0.4f, 1f, 0.4f, 0.6f);
        [SerializeField] private Color invalidSiteColor = new Color(1f, 0.3f, 0.3f, 0.6f);

        [SerializeField] private int bodySortingOrder = -30;
        [SerializeField] private int overlaySortingOrder = 55;

        public Sprite SquareSprite => squareSprite;

        public Sprite CircleSprite => circleSprite;

        public Vector2 HeadquartersSize => headquartersSize;

        public Vector2 BarracksSize => barracksSize;

        public float HeadquartersInnerScale => headquartersInnerScale;

        public Color InnerColor => innerColor;

        public float ConstructionAlpha => constructionAlpha;

        public Vector2 ProgressBarSize => progressBarSize;

        public float ProgressBarOffset => progressBarOffset;

        public Color ProgressBackground => progressBackground;

        public Color ProgressColor => progressColor;

        public Vector2 QueueDotSize => queueDotSize;

        public float QueueDotSpacing => queueDotSpacing;

        public Color SelectionColor => selectionColor;

        public float SelectionPadding => selectionPadding;

        public Color BuildRadiusColor => buildRadiusColor;

        public Color ValidSiteColor => validSiteColor;

        public Color InvalidSiteColor => invalidSiteColor;

        public int BodySortingOrder => bodySortingOrder;

        public int OverlaySortingOrder => overlaySortingOrder;
    }
}
