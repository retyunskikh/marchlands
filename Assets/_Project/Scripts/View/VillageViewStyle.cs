using System;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>Внешний вид деревень-заглушек. Цвета сторон берутся из стиля отрядов.</summary>
    [Serializable]
    public sealed class VillageViewStyle
    {
        [SerializeField] private Sprite houseSprite;
        [SerializeField] private Sprite circleSprite;
        [SerializeField] private Sprite squareSprite;
        [Tooltip("Расположение домиков относительно центра деревни, u.")]
        [SerializeField] private Vector2[] houseOffsets =
        {
            new Vector2(-1.1f, 0.6f), new Vector2(0.9f, 0.9f), new Vector2(0.2f, -0.5f), new Vector2(-0.6f, -1.3f)
        };
        [SerializeField] private float houseSize = 1.1f;
        [SerializeField] private Color houseColor = new Color(0.72f, 0.55f, 0.38f);
        [SerializeField] private Color neutralColor = new Color(0.6f, 0.6f, 0.6f);
        [Tooltip("Прозрачность круга радиуса захвата.")]
        [SerializeField] private float radiusAlpha = 0.35f;
        [SerializeField] private Vector2 flagSize = new Vector2(0.9f, 0.6f);
        [SerializeField] private Vector2 flagOffset = new Vector2(1.6f, 1.6f);
        [SerializeField] private Vector2 progressBarSize = new Vector2(2.4f, 0.22f);
        [Tooltip("Насколько выше центра деревни стоит полоска захвата, u.")]
        [SerializeField] private float progressBarOffset = 2.6f;
        [SerializeField] private Color progressBackground = new Color(0f, 0f, 0f, 0.45f);
        [SerializeField] private int groundSortingOrder = -50;
        [SerializeField] private int houseSortingOrder = -40;
        [SerializeField] private int overlaySortingOrder = 55;

        public Sprite HouseSprite => houseSprite;

        public Sprite CircleSprite => circleSprite;

        public Sprite SquareSprite => squareSprite;

        public Vector2[] HouseOffsets => houseOffsets;

        public float HouseSize => houseSize;

        public Color HouseColor => houseColor;

        public Color NeutralColor => neutralColor;

        public float RadiusAlpha => radiusAlpha;

        public Vector2 FlagSize => flagSize;

        public Vector2 FlagOffset => flagOffset;

        public Vector2 ProgressBarSize => progressBarSize;

        public float ProgressBarOffset => progressBarOffset;

        public Color ProgressBackground => progressBackground;

        public int GroundSortingOrder => groundSortingOrder;

        public int HouseSortingOrder => houseSortingOrder;

        public int OverlaySortingOrder => overlaySortingOrder;
    }
}
