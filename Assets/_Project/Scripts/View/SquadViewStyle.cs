using System;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;
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
        [Tooltip("Во сколько раз фигурки солдат крупнее исходного спрайта (только вид; интервалы строя — в конфиге).")]
        [SerializeField] private float soldierScale = 1f;
        [Tooltip("Рекрут, ждущий у казармы.")]
        [SerializeField] private Sprite recruitSprite;
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
        [Tooltip("«Целься!»: на сколько юнитов стреляющие выносят ружьё вперёд и за сколько секунд вскидывают и опускают его.")]
        [SerializeField] private float aimReach = 0.12f;
        [SerializeField] private float aimRaiseTime = 0.3f;
        [SerializeField] private float aimLowerTime = 0.35f;
        [Tooltip("Выпадов в секунду у каждого солдата.")]
        [SerializeField] private float thrustRate = 1.1f;

        [Tooltip("Как быстро строй смыкается после потерь, 1/s: солдаты не телепортируются на места павших.")]
        [SerializeField] private float regroupSpeed = 3f;

        [Header("Численность отряда")]
        [Tooltip("Писать численность числом у отряда; без него остаются «+1» при пополнении и полоска пополнения.")]
        [SerializeField] private bool showCount;
        [Tooltip("Высота цифр численности, u.")]
        [SerializeField] private float countSize = 0.55f;
        [Tooltip("Насколько левее полоски морали стоят цифры, u.")]
        [SerializeField] private float countGap = 0.9f;
        [SerializeField] private Color countColor = new Color(1f, 1f, 0.95f);
        [SerializeField] private Color countGainColor = new Color(0.45f, 1f, 0.45f);
        [SerializeField] private Color countLossColor = new Color(1f, 0.35f, 0.3f);
        [Tooltip("Сколько цифры зелёные и подпрыгивают после пополнения, s.")]
        [SerializeField] private float countGainTime = 1.2f;
        [SerializeField] private float countLossTime = 0.6f;
        [SerializeField] private float countBounce = 0.25f;
        [Tooltip("Цвет числа, пока отряд пополняется в своей деревне.")]
        [SerializeField] private Color countReinforceColor = new Color(0.7f, 1f, 0.6f);
        [Tooltip("Полоска до следующего человека из деревни под числом: ширина и высота в юнитах.")]
        [SerializeField] private Vector2 reinforceBarSize = new Vector2(1f, 0.14f);
        [SerializeField] private Color reinforceBarBack = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color reinforceBarFill = new Color(1f, 0.8f, 0.3f);
        [Tooltip("Домик над полоской пополнения: отряд стоит в своей деревне и пополняется.")]
        [SerializeField] private Sprite reinforceIcon;
        [SerializeField] private float reinforceIconSize = 0.9f;
        [Tooltip("Сколько всплывает «+1», s, и на сколько поднимается, u.")]
        [SerializeField] private float floaterLifetime = 1.4f;
        [SerializeField] private float floaterRise = 1.6f;

        [Header("Походка (только вид)")]
        [Tooltip("Гренадёры маршируют в ногу: шагов в секунду, покачивание вбок, u.")]
        [SerializeField] private Vector2 marchGait = new Vector2(1.8f, 0.035f);
        [Tooltip("Конница скачет: скачков в секунду, рывок вперёд-назад, u.")]
        [SerializeField] private Vector2 gallopGait = new Vector2(2.4f, 0.09f);
        [Tooltip("Длина полного цикла шага (левой-правой) пехоты при скорости 1 u/s; с ростом скорости шаг длиннее (× √скорости) — "
            + "частота шагов растёт медленнее скорости, и ноги не скользят по земле, u.")]
        [SerializeField] private float marchStride = 0.6f;
        [Tooltip("То же для конницы: длина цикла скачка при скорости 1 u/s, u.")]
        [SerializeField] private float gallopStride = 0.75f;
        [Header("Штурм здания")]
        [Tooltip("За сколько секунд строй рассыпается вокруг здания на штурме (и собирается обратно), s.")]
        [SerializeField] private float assaultBlendTime = 0.8f;
        [Tooltip("Дуга, по которой штурмующие обступают здание со своей стороны, градусы (360 — кольцом).")]
        [SerializeField] private float assaultArc = 220f;
        [Tooltip("Насколько от стены стоит первая волна штурмующих, u.")]
        [SerializeField] private float assaultStandOff = 0.35f;
        [Tooltip("Насколько дальше от стены каждая следующая шеренга, u.")]
        [SerializeField] private float assaultRankGap = 0.55f;
        [Tooltip("Доля первой волны, что лезет на стены.")]
        [SerializeField] private float assaultClimbShare = 0.3f;
        [Tooltip("Цикл лазания: вскарабкался, повисел, сорвался, s.")]
        [SerializeField] private float assaultClimbPeriod = 2.2f;
        [Tooltip("Насколько лезущий заходит на здание от стены, u.")]
        [SerializeField] private float assaultClimbDepth = 0.6f;
        [Tooltip("Насколько лезущий крупнее наверху (он ближе к зрителю), доля.")]
        [SerializeField] private float assaultClimbScale = 0.25f;
        [Tooltip("Размах толкотни у стены вбок, u.")]
        [SerializeField] private float assaultSway = 0.12f;
        [Tooltip("За сколько секунд походка появляется и стихает.")]
        [SerializeField] private float gaitBlendTime = 0.25f;
        [Tooltip("С какой скорости солдат шагает, а не стоит, u/s: при развороте строя шагают и фланги, и почти стоящая середина.")]
        [SerializeField] private float stepMinSpeed = 0.12f;
        [Tooltip("Одиночный пешеход (рекрут, пополнение, рабочий): шагов в секунду и покачивание вбок, u.")]
        [SerializeField] private Vector2 walkerGait = new Vector2(2.2f, 0.03f);

        [Tooltip("Строй конницы: лошади шире и длиннее пехотинца (только вид).")]
        [SerializeField] private Vector2 cavalrySpacing = new Vector2(1.3f, 1.4f);

        [Header("Пушки")]
        [Tooltip("Пушка сверху, стволом вверх (+Y).")]
        [SerializeField] private Sprite gunSprite;
        [Tooltip("Передок (зарядный ящик на колёсах) сверху, дышлом вверх.")]
        [SerializeField] private Sprite limberSprite;
        [SerializeField] private float gunScale = 1.7f;
        [SerializeField] private float limberScale = 1.4f;
        [Tooltip("Насколько впереди расчёта стоит снятая с передка пушка, u.")]
        [SerializeField] private float gunAhead = 0.9f;
        [Tooltip("Насколько впереди расчёта едет передок (пушка на марше — за ним, стволом назад), u.")]
        [SerializeField] private float limberAhead = 2.2f;
        [SerializeField] private Color franceColor = new Color(0.2f, 0.35f, 0.85f);
        [SerializeField] private Color austriaColor = new Color(0.95f, 0.95f, 0.95f);
        [SerializeField] private Color selectionColor = new Color(1f, 0.9f, 0.3f, 0.3f);
        [Tooltip("Брошенная пушка разбитого расчёта: сколько лежит, за сколько тает (s), насколько темнее.")]
        [SerializeField] private float abandonedGunLieTime = 8f;
        [SerializeField] private float abandonedGunFadeTime = 3f;
        [SerializeField] private float abandonedGunDarkening = 0.7f;
        [Tooltip("Подсветка цели выбранной пушки (мерцает).")]
        [SerializeField] private Color gunTargetColor = new Color(1f, 0.25f, 0.15f, 0.35f);
        [Tooltip("Насколько рамка выделения шире габаритов отряда с каждой стороны, u.")]
        [SerializeField] private float selectionPadding = 0.3f;
        [SerializeField] private int markerSortingOrder = 10;
        [Header("Маршрут дальнего марша")]
        [Tooltip("Пунктир маршрута у выделенного отряда: шаг точек, u, их размер и цвет.")]
        [SerializeField] private float routeDotSpacing = 1.2f;
        [SerializeField] private float routeDotSize = 0.28f;
        [SerializeField] private Color routeDotColor = new Color(1f, 0.92f, 0.45f, 0.75f);
        [SerializeField] private int routeDotsMax = 160;
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

        public float SoldierScale => soldierScale;

        public float MeleeBlendTime => meleeBlendTime;

        public float GaitBlendTime => gaitBlendTime;

        public float StepMinSpeed => stepMinSpeed;

        public Vector2 WalkerGait => walkerGait;

        public bool ShowCount => showCount;

        public float CountSize => countSize;

        public float CountGap => countGap;

        public Color CountColor => countColor;

        public Color CountGainColor => countGainColor;

        public Color CountLossColor => countLossColor;

        public float CountGainTime => countGainTime;

        public float CountLossTime => countLossTime;

        public float CountBounce => countBounce;

        public Color CountReinforceColor => countReinforceColor;

        public Vector2 ReinforceBarSize => reinforceBarSize;

        public Color ReinforceBarBack => reinforceBarBack;

        public Color ReinforceBarFill => reinforceBarFill;

        public Sprite ReinforceIcon => reinforceIcon;

        public float ReinforceIconSize => reinforceIconSize;

        public float FloaterLifetime => floaterLifetime;

        public float FloaterRise => floaterRise;

        public float RegroupSpeed => regroupSpeed;

        /// <summary>Частота шага (в секунду) и размах покачивания (u) для рода войск.</summary>
        public Vector2 GetGait(UnitType type)
        {
            switch (type)
            {
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

        public float AimReach => aimReach;

        public float AimRaiseTime => aimRaiseTime;

        public float AimLowerTime => aimLowerTime;

        public float ThrustRate => thrustRate;

        /// <summary>Длина цикла шага рода войск при скорости <paramref name="speed"/>, u.</summary>
        public float AssaultBlendTime => assaultBlendTime;
        public float AssaultArc => assaultArc;
        public float AssaultStandOff => assaultStandOff;
        public float AssaultRankGap => assaultRankGap;
        public float AssaultClimbShare => assaultClimbShare;
        public float AssaultClimbPeriod => assaultClimbPeriod;
        public float AssaultClimbDepth => assaultClimbDepth;
        public float AssaultClimbScale => assaultClimbScale;
        public float AssaultSway => assaultSway;

        public float GetStride(UnitType type, float speed)
        {
            float baseStride = type == UnitType.Cavalry ? gallopStride : marchStride;
            return math.max(0.05f, baseStride * math.sqrt(math.max(speed, 0.25f)));
        }

        /// <summary>Множитель раскладки строя по осям (поперёк фронта, вдоль) для рода войск.</summary>
        public Vector2 GetSpacing(UnitType type)
        {
            switch (type)
            {
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

        public Color GunTargetColor => gunTargetColor;

        public float AbandonedGunLieTime => abandonedGunLieTime;

        public float AbandonedGunFadeTime => abandonedGunFadeTime;

        public float AbandonedGunDarkening => abandonedGunDarkening;

        public float SelectionPadding => selectionPadding;

        public int MarkerSortingOrder => markerSortingOrder;

        public float RouteDotSpacing => routeDotSpacing;

        public float RouteDotSize => routeDotSize;

        public Color RouteDotColor => routeDotColor;

        public int RouteDotsMax => routeDotsMax;

        public int SoldierSortingOrder => soldierSortingOrder;

        public Sprite GunSprite => gunSprite;

        public Sprite LimberSprite => limberSprite;

        public float GunScale => gunScale;

        public float LimberScale => limberScale;

        public float GunAhead => gunAhead;

        public float LimberAhead => limberAhead;

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
