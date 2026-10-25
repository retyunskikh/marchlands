using System;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>Внешний вид отрядов-заглушек. Это не баланс: на симуляцию не влияет.</summary>
    [Serializable]
    public sealed class SquadViewStyle
    {
        [Tooltip("Запасная фигурка, окрашиваемая цветом стороны, — если нарисованных нет.")]
        [SerializeField] private Sprite soldierSprite;
        [SerializeField] private Sprite markerSprite;
        [SerializeField] private SoldierSprites franceSoldiers = new SoldierSprites();
        [SerializeField] private SoldierSprites austriaSoldiers = new SoldierSprites();
        [Tooltip("Рекрут, ждущий у казармы.")]
        [SerializeField] private Sprite recruitSprite;
        [Tooltip("Рассыпной строй егерей: во сколько раз шире интервалы и дистанции (только вид).")]
        [SerializeField] private Vector2 jaegerSpacing = new Vector2(1.6f, 1.3f);
        [Tooltip("Насколько егерь сбивается со своего места в рассыпном строю, u.")]
        [SerializeField] private float jaegerScatter = 0.12f;
        [SerializeField] private Color franceColor = new Color(0.2f, 0.35f, 0.85f);
        [SerializeField] private Color austriaColor = new Color(0.95f, 0.95f, 0.95f);
        [SerializeField] private Color selectionColor = new Color(1f, 0.9f, 0.3f, 0.3f);
        [Tooltip("Насколько рамка выделения шире габаритов отряда с каждой стороны, u.")]
        [SerializeField] private float selectionPadding = 0.3f;
        [SerializeField] private int markerSortingOrder = 10;
        [SerializeField] private int soldierSortingOrder = 20;

        [Header("Кольцо перезарядки")]
        [SerializeField] private int ringSegments = 24;
        [SerializeField] private float ringRadius = 0.6f;
        [Tooltip("x — толщина сегмента по радиусу, y — длина по окружности, u.")]
        [SerializeField] private Vector2 ringSegmentSize = new Vector2(0.14f, 0.13f);
        [Tooltip("Зазор между полоской морали и кольцом, u.")]
        [SerializeField] private float ringGap = 0.4f;
        [SerializeField] private Color ringLoadedColor = new Color(0.45f, 0.95f, 0.45f);
        [SerializeField] private Color ringAimingColor = new Color(1f, 0.6f, 0.2f);
        [SerializeField] private Color ringReloadingColor = new Color(1f, 1f, 1f);
        [SerializeField] private Color ringEmptyColor = new Color(0f, 0f, 0f, 0.35f);
        [SerializeField] private int ringSortingOrder = 50;

        [Header("Полоска морали")]
        [SerializeField] private Vector2 moraleBarSize = new Vector2(1.6f, 0.16f);
        [Tooltip("Зазор между тылом отряда и полоской, u.")]
        [SerializeField] private float moraleBarGap = 0.3f;
        [SerializeField] private Color moraleBarBackground = new Color(0f, 0f, 0f, 0.45f);
        [SerializeField] private Color moraleHighColor = new Color(0.45f, 0.95f, 0.45f);
        [SerializeField] private Color moraleLowColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField] private Color moraleRoutingColor = new Color(0.95f, 0.25f, 0.2f);

        [Header("Бегство")]
        [Tooltip("Во сколько раз раздвигается строй бегущего отряда.")]
        [SerializeField] private float routSpread = 1.5f;
        [Tooltip("Насколько каждый бегущий сбивается со своего места, u.")]
        [SerializeField] private float routScatter = 0.35f;

        public Sprite SoldierSprite => soldierSprite;

        public Sprite MarkerSprite => markerSprite;

        public Sprite RecruitSprite => recruitSprite;

        public float JaegerScatter => jaegerScatter;

        /// <summary>Множитель раскладки строя по осям (поперёк фронта, вдоль) для рода войск.</summary>
        public Vector2 GetSpacing(UnitType type)
        {
            return type == UnitType.Jaegers ? jaegerSpacing : Vector2.one;
        }

        /// <summary>Нарисованная фигурка; null — рисовать запасной, окрашенной в цвет стороны.</summary>
        public Sprite GetSoldierSprite(Side side, UnitType type, SoldierRole role)
        {
            SoldierSprites sprites = side == Side.France ? franceSoldiers : austriaSoldiers;
            return sprites != null && sprites.IsComplete ? sprites.Get(type, role) : null;
        }

        public Color SelectionColor => selectionColor;

        public float SelectionPadding => selectionPadding;

        public int MarkerSortingOrder => markerSortingOrder;

        public int SoldierSortingOrder => soldierSortingOrder;

        public int RingSegments => ringSegments;

        public float RingRadius => ringRadius;

        public Vector2 RingSegmentSize => ringSegmentSize;

        public float RingGap => ringGap;

        public Color RingLoadedColor => ringLoadedColor;

        public Color RingAimingColor => ringAimingColor;

        public Color RingReloadingColor => ringReloadingColor;

        public Color RingEmptyColor => ringEmptyColor;

        public int RingSortingOrder => ringSortingOrder;

        public Vector2 MoraleBarSize => moraleBarSize;

        public float MoraleBarGap => moraleBarGap;

        public Color MoraleBarBackground => moraleBarBackground;

        public Color MoraleRoutingColor => moraleRoutingColor;

        public float RoutSpread => routSpread;

        public float RoutScatter => routScatter;

        /// <summary>Цвет полоски держащегося отряда: от жёлтого при низкой морали к зелёному при высокой.</summary>
        public Color GetSteadyMoraleColor(float morale01)
        {
            return Color.Lerp(moraleLowColor, moraleHighColor, morale01);
        }

        public Color GetSideColor(Side side)
        {
            return side == Side.France ? franceColor : austriaColor;
        }
    }
}
