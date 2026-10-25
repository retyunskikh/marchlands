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
        [SerializeField] private Sprite discSprite;
        [Tooltip("Расположение домиков относительно центра деревни, u.")]
        [SerializeField] private Vector2[] houseOffsets =
        {
            new Vector2(-1.1f, 0.6f), new Vector2(0.9f, 0.9f), new Vector2(0.2f, -0.5f), new Vector2(-0.6f, -1.3f)
        };
        [SerializeField] private float houseSize = 1.1f;
        [SerializeField] private Color houseColor = new Color(0.72f, 0.55f, 0.38f);
        [SerializeField] private Color neutralColor = new Color(0.6f, 0.6f, 0.6f);

        [Header("Зона захвата")]
        [Tooltip("Прозрачность заливки зоны цветом владельца.")]
        [SerializeField] private float zoneFillAlpha = 0.18f;
        [Tooltip("Прозрачность контура зоны.")]
        [SerializeField] private float zoneOutlineAlpha = 0.55f;

        [Header("Кольцо захвата")]
        [SerializeField] private int ringSegments = 48;
        [Tooltip("Толщина кольца по радиусу, u.")]
        [SerializeField] private float ringThickness = 0.5f;
        [Tooltip("Доля дуги, занятая сегментом (остальное — зазор).")]
        [SerializeField] private float ringSegmentFill = 0.8f;
        [SerializeField] private Color ringEmptyColor = new Color(0f, 0f, 0f, 0.35f);
        [SerializeField] private Color contestedColor = new Color(1f, 0.85f, 0.2f);
        [Tooltip("Сколько раз в секунду мигает кольцо оспариваемой деревни.")]
        [SerializeField] private float contestedBlinkRate = 2.5f;
        [Tooltip("Сколько секунд вспыхивает кольцо после захвата.")]
        [SerializeField] private float captureFlashDuration = 1.2f;

        [Header("Флаг")]
        [SerializeField] private Vector2 flagSize = new Vector2(1.4f, 0.9f);
        [SerializeField] private Vector2 flagOffset = new Vector2(1.9f, 2f);
        [SerializeField] private Vector2 poleSize = new Vector2(0.12f, 2f);
        [SerializeField] private Color poleColor = new Color(0.25f, 0.2f, 0.15f);

        [SerializeField] private int groundSortingOrder = -50;
        [SerializeField] private int houseSortingOrder = -40;
        [SerializeField] private int overlaySortingOrder = 55;

        public Sprite HouseSprite => houseSprite;

        public Sprite CircleSprite => circleSprite;

        public Sprite SquareSprite => squareSprite;

        public Sprite DiscSprite => discSprite;

        public Vector2[] HouseOffsets => houseOffsets;

        public float HouseSize => houseSize;

        public Color HouseColor => houseColor;

        public Color NeutralColor => neutralColor;

        public float ZoneFillAlpha => zoneFillAlpha;

        public float ZoneOutlineAlpha => zoneOutlineAlpha;

        public int RingSegments => ringSegments;

        public float RingThickness => ringThickness;

        public float RingSegmentFill => ringSegmentFill;

        public Color RingEmptyColor => ringEmptyColor;

        public Color ContestedColor => contestedColor;

        public float ContestedBlinkRate => contestedBlinkRate;

        public float CaptureFlashDuration => captureFlashDuration;

        public Vector2 FlagSize => flagSize;

        public Vector2 FlagOffset => flagOffset;

        public Vector2 PoleSize => poleSize;

        public Color PoleColor => poleColor;

        public int GroundSortingOrder => groundSortingOrder;

        public int HouseSortingOrder => houseSortingOrder;

        public int OverlaySortingOrder => overlaySortingOrder;
    }
}
