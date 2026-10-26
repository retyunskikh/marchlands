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
        [Header("Рукопашная (только вид)")]
        [Tooltip("За сколько секунд строй сходится в свалку и расходится обратно.")]
        [SerializeField] private float meleeBlendTime = 0.6f;
        [Tooltip("Насколько сжимается строй к линии схватки: 0 — не сжимается, 1 — все на линии.")]
        [SerializeField] private float meleeCompress = 0.6f;
        [Tooltip("На каком расстоянии от линии схватки стоит первый ряд, u.")]
        [SerializeField] private float meleeGap = 0.3f;
        [Tooltip("Насколько каждый сбивается со своего места в свалке, u.")]
        [SerializeField] private float meleeScatter = 0.3f;
        [Tooltip("Длина выпада штыком или саблей, u.")]
        [SerializeField] private float thrustLength = 0.16f;
        [Tooltip("Выпадов в секунду у каждого солдата.")]
        [SerializeField] private float thrustRate = 1.1f;

        [Header("Походка (только вид)")]
        [Tooltip("Гренадёры маршируют в ногу: шагов в секунду, покачивание вбок, u.")]
        [SerializeField] private Vector2 marchGait = new Vector2(1.8f, 0.035f);
        [Tooltip("Егеря бегут вразнобой: шагов в секунду, покачивание, u.")]
        [SerializeField] private Vector2 runGait = new Vector2(3f, 0.05f);
        [Tooltip("Конница скачет: скачков в секунду, рывок вперёд-назад, u.")]
        [SerializeField] private Vector2 gallopGait = new Vector2(2.4f, 0.09f);
        [Tooltip("За сколько секунд походка появляется и стихает.")]
        [SerializeField] private float gaitBlendTime = 0.25f;

        [Tooltip("Строй конницы: лошади шире и длиннее пехотинца (только вид).")]
        [SerializeField] private Vector2 cavalrySpacing = new Vector2(1.3f, 1.4f);
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

        public float MeleeBlendTime => meleeBlendTime;

        public float GaitBlendTime => gaitBlendTime;

        /// <summary>Частота шага (в секунду) и размах покачивания (u) для рода войск.</summary>
        public Vector2 GetGait(UnitType type)
        {
            switch (type)
            {
                case UnitType.Jaegers:
                    return runGait;
                case UnitType.Cavalry:
                    return gallopGait;
                default:
                    return marchGait;
            }
        }

        public float MeleeCompress => meleeCompress;

        public float MeleeGap => meleeGap;

        public float MeleeScatter => meleeScatter;

        public float ThrustLength => thrustLength;

        public float ThrustRate => thrustRate;

        /// <summary>Множитель раскладки строя по осям (поперёк фронта, вдоль) для рода войск.</summary>
        public Vector2 GetSpacing(UnitType type)
        {
            switch (type)
            {
                case UnitType.Jaegers:
                    return jaegerSpacing;
                case UnitType.Cavalry:
                    return cavalrySpacing;
                default:
                    return Vector2.one;
            }
        }

        public Sprite GetWalkSprite(Side side, UnitType type, SoldierRole role, int frame)
        {
            SoldierSprites sprites = side == Side.France ? franceSoldiers : austriaSoldiers;
            return sprites?.GetWalk(type, role, frame);
        }

        public bool TryGetMeleeSprites(Side side, UnitType type, SoldierRole role, out Sprite body, out Sprite arms)
        {
            SoldierSprites sprites = side == Side.France ? franceSoldiers : austriaSoldiers;
            body = null;
            arms = null;
            return sprites != null && sprites.TryGetMelee(type, role, out body, out arms);
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
