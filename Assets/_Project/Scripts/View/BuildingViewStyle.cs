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
        [SerializeField] private Vector2 stableSize = new Vector2(3.6f, 2.6f);
        [Tooltip("Доля тёмной внутренней части штаба.")]
        [SerializeField] private float headquartersInnerScale = 0.55f;
        [SerializeField] private Color innerColor = new Color(0.15f, 0.15f, 0.18f);

        [Header("Штаб")]
        [Tooltip("Усадьба без крыш: ограда, двор, фонтан. Ворота справа.")]
        [SerializeField] private Sprite headquartersBaseSprite;
        [Tooltip("Крыши в оттенках серого — окрашиваются цветом стороны.")]
        [SerializeField] private Sprite headquartersRoofSprite;
        [Tooltip("Где во дворе стоит флаг, доля размера штаба от центра (для ворот справа).")]
        [SerializeField] private Vector2 headquartersFlagPosition = new Vector2(0.08f, 0.15f);
        [SerializeField] private Vector2 headquartersFlagSize = new Vector2(0.7f, 0.42f);
        [SerializeField] private float headquartersPoleSize = 0.14f;
        [SerializeField] private Color poleColor = new Color(0.25f, 0.2f, 0.15f);

        [Header("Казарма")]
        [Tooltip("Плац с оградой, колодцем и козлами с ружьями. Ворота справа.")]
        [SerializeField] private Sprite barracksBaseSprite;
        [Tooltip("Крыши казармы и караульни в оттенках серого — окрашиваются цветом стороны.")]
        [SerializeField] private Sprite barracksRoofSprite;

        [Header("Конюшня")]
        [Tooltip("Левада с изгородью, коновязь, сено. Ворота справа.")]
        [SerializeField] private Sprite stableBaseSprite;
        [Tooltip("Крыши конюшни в оттенках серого — окрашиваются цветом стороны.")]
        [SerializeField] private Sprite stableRoofSprite;
        [Tooltip("Прозрачность строящегося здания.")]
        [SerializeField] private float constructionAlpha = 0.45f;
        [SerializeField] private Vector2 progressBarSize = new Vector2(2.6f, 0.22f);
        [Tooltip("Насколько выше центра здания стоит полоска постройки или найма, u.")]
        [SerializeField] private float progressBarOffset = 2f;
        [SerializeField] private Color progressBackground = new Color(0f, 0f, 0f, 0.45f);
        [SerializeField] private Color progressColor = new Color(1f, 0.85f, 0.45f);
        [Tooltip("Цвет полоски захвата штаба врагом.")]
        [SerializeField] private Color captureColor = new Color(0.95f, 0.25f, 0.2f);
        [SerializeField] private Color selectionColor = new Color(1f, 0.9f, 0.3f, 0.35f);
        [SerializeField] private float selectionPadding = 0.4f;

        [Header("Рекруты у казармы")]
        [Tooltip("Больше этого ждущих людей не рисуется.")]
        [SerializeField] private int recruitsShownMax = 32;
        [Tooltip("Где стоит толпа: на сколько дальше края казармы в сторону фронта, u.")]
        [SerializeField] private float recruitsOffset = 0.9f;
        [SerializeField] private Vector2 recruitsSpacing = new Vector2(0.45f, 0.45f);
        [SerializeField] private int recruitsPerRow = 8;

        [Header("Предпросмотр постройки")]
        [SerializeField] private Color buildRadiusColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color validSiteColor = new Color(0.4f, 1f, 0.4f, 0.6f);
        [SerializeField] private Color invalidSiteColor = new Color(1f, 0.3f, 0.3f, 0.6f);

        [SerializeField] private int bodySortingOrder = -30;
        [SerializeField] private int overlaySortingOrder = 55;

        public Sprite SquareSprite => squareSprite;

        public Sprite HeadquartersBaseSprite => headquartersBaseSprite;

        public Sprite HeadquartersRoofSprite => headquartersRoofSprite;

        public Vector2 HeadquartersFlagPosition => headquartersFlagPosition;

        public Vector2 HeadquartersFlagSize => headquartersFlagSize;

        public float HeadquartersPoleSize => headquartersPoleSize;

        public Color PoleColor => poleColor;

        public Sprite BarracksBaseSprite => barracksBaseSprite;

        public Sprite StableBaseSprite => stableBaseSprite;

        public Sprite StableRoofSprite => stableRoofSprite;

        public Vector2 StableSize => stableSize;

        public int RecruitsShownMax => recruitsShownMax;

        public float RecruitsOffset => recruitsOffset;

        public Vector2 RecruitsSpacing => recruitsSpacing;

        public int RecruitsPerRow => recruitsPerRow;

        public Sprite BarracksRoofSprite => barracksRoofSprite;

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

        public Color CaptureColor => captureColor;

        public Color SelectionColor => selectionColor;

        public float SelectionPadding => selectionPadding;

        public Color BuildRadiusColor => buildRadiusColor;

        public Color ValidSiteColor => validSiteColor;

        public Color InvalidSiteColor => invalidSiteColor;

        public int BodySortingOrder => bodySortingOrder;

        public int OverlaySortingOrder => overlaySortingOrder;
    }
}
