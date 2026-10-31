using System;
using Marchlands.Simulation.Buildings;
using UnityEngine;
using UnityEngine.Serialization;

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
        [Header("Дома-пристройки к штабу")]
        [SerializeField] private Sprite annexBaseSprite;
        [Tooltip("Крыша пристройки — тонируется цветом стороны, как крыши штаба.")]
        [SerializeField] private Sprite annexRoofSprite;
        [SerializeField] private Vector2 annexSize = new Vector2(2.4f, 1.6f);
        [Tooltip("Места пристроек в порядке постройки: от центра штаба, ворота — по +X (у правого штаба отражается).")]
        [SerializeField] private Vector2[] annexOffsets =
        {
            new Vector2(-1.2f, 3.3f), new Vector2(-1.2f, -3.3f), new Vector2(1.4f, 3.3f), new Vector2(1.4f, -3.3f)
        };

        [SerializeField] private Sprite barracksBaseSprite;
        [Tooltip("Крыши казармы и караульни в оттенках серого — окрашиваются цветом стороны.")]
        [SerializeField] private Sprite barracksRoofSprite;
        [Tooltip("Двери дома казармы, доля размера от центра (для ворот справа): отсюда выходят новые рекруты и сюда уходят распущенные.")]
        [SerializeField] private Vector2[] barracksDoors =
        {
            new Vector2(-0.19f, 0f), new Vector2(-0.13f, 0f), new Vector2(-0.07f, 0f), new Vector2(-0.02f, 0f),
            new Vector2(0.04f, 0f), new Vector2(0.1f, 0f), new Vector2(0.16f, 0f), new Vector2(0.22f, 0f)
        };
        [Tooltip("Ворота плаца казармы, доля размера от центра (для ворот справа).")]
        [SerializeField] private Vector2 barracksGate = new Vector2(0.5f, 0f);
        [Tooltip("На сколько до и после ворот рекрут проходит прямо сквозь них, u.")]
        [SerializeField] private float gatePassDepth = 0.35f;

        [Header("Конюшня")]
        [Tooltip("Левада с изгородью, коновязь, сено. Ворота справа.")]
        [SerializeField] private Sprite stableBaseSprite;
        [Tooltip("Крыши конюшни в оттенках серого — окрашиваются цветом стороны.")]
        [SerializeField] private Sprite stableRoofSprite;
        [Tooltip("Прозрачность строящегося здания.")]
        [SerializeField] private float constructionAlpha = 0.45f;
        [SerializeField] private Vector2 progressBarSize = new Vector2(2.6f, 0.22f);
        [Tooltip("Насколько выше верхнего края здания стоит полоска постройки или найма, u.")]
        [SerializeField] private float progressBarOffset = 0.7f;
        [SerializeField] private Color progressBackground = new Color(0f, 0f, 0f, 0.45f);
        [SerializeField] private Color progressColor = new Color(1f, 0.85f, 0.45f);
        [Tooltip("Цвет полоски здоровья повреждённого здания; она стоит над полоской постройки или найма.")]
        [FormerlySerializedAs("captureColor")]
        [SerializeField] private Color healthColor = new Color(0.95f, 0.25f, 0.2f);
        [SerializeField] private Color selectionColor = new Color(1f, 0.9f, 0.3f, 0.35f);
        [SerializeField] private float selectionPadding = 0.4f;

        [Header("Рекруты у казармы")]
        [Tooltip("Больше этого ждущих людей не рисуется.")]
        [SerializeField] private int recruitsShownMax = 32;
        [Tooltip("Где стоит толпа: на сколько дальше края казармы в сторону фронта, u.")]
        [SerializeField] private float recruitsOffset = 0.9f;
        [Tooltip("С какой скоростью новый рекрут идёт от ворот здания к своему месту в толпе, u/s.")]
        [SerializeField] private float recruitWalkSpeed = 2.5f;
        [SerializeField] private Vector2 recruitsSpacing = new Vector2(0.45f, 0.45f);
        [SerializeField] private int recruitsPerRow = 5;
        [Tooltip("Фузилёры и гренадёры ждут двумя группами по сторонам от середины толпы; промежуток между группами, u.")]
        [SerializeField] private float recruitsGroupGap = 1f;

        [Header("Повреждения")]
        [Tooltip("Цвет, в который темнеет здание по мере потери здоровья (у разрушенного почти до конца — полностью).")]
        [SerializeField] private Color damagedTint = new Color(0.45f, 0.38f, 0.32f);
        [Tooltip("Места дыма, огня и проломов, доля размера от центра (для ворот справа).")]
        [SerializeField] private Vector2[] damageSpots =
        {
            new Vector2(-0.25f, 0.25f), new Vector2(0.2f, 0.3f), new Vector2(0.05f, -0.2f), new Vector2(-0.3f, -0.25f)
        };
        [Tooltip("Ниже этой доли здоровья здание дымит и в крыше появляются проломы.")]
        [SerializeField] private float smokeHealth = 0.66f;
        [Tooltip("Ниже этой доли здоровья здание горит.")]
        [SerializeField] private float fireHealth = 0.33f;
        [SerializeField] private Sprite smokeSprite;
        [SerializeField] private Color smokeColor = new Color(0.18f, 0.17f, 0.16f, 0.85f);
        [Tooltip("Размер клуба дыма: у основания и в конце подъёма, u.")]
        [SerializeField] private Vector2 smokeSize = new Vector2(1.1f, 2.8f);
        [Tooltip("На сколько поднимается клуб дыма, u.")]
        [SerializeField] private float smokeRise = 1.4f;
        [Tooltip("За сколько секунд клуб дыма поднимается и тает, s.")]
        [SerializeField] private float smokePeriod = 2.6f;
        [SerializeField] private Color fireColor = new Color(1f, 0.55f, 0.15f, 0.9f);
        [SerializeField] private float fireSize = 0.9f;
        [SerializeField] private Color holeColor = new Color(0.08f, 0.06f, 0.05f, 0.95f);
        [SerializeField] private float holeSize = 1.1f;

        [Header("Часовые")]
        [Tooltip("Круг дальности часовых — у выделенного здания и у здания, на которое идёт штурм.")]
        [SerializeField] private Color sentryRangeColor = new Color(1f, 0.35f, 0.25f, 0.35f);
        [Tooltip("Посты часовых на углах ограды, доля размера от центра.")]
        [SerializeField] private Vector2[] sentryPosts =
        {
            new Vector2(0.47f, 0.43f), new Vector2(0.47f, -0.43f), new Vector2(-0.47f, 0.43f), new Vector2(-0.47f, -0.43f)
        };
        [Tooltip("Сколько секунд после залпа часовой смотрит на цель, s.")]
        [SerializeField] private float sentryAlertTime = 3f;
        [Tooltip("Размер часового относительно солдата отряда.")]
        [SerializeField] private float sentryScale = 0.9f;

        [Header("Рабочие")]
        [SerializeField] private Color workerColor = new Color(0.78f, 0.62f, 0.42f);
        [SerializeField] private int workersPerBuilding = 3;
        [SerializeField] private float workerSpeed = 2f;
        [Tooltip("На сколько рабочий отходит от стены здания, u.")]
        [SerializeField] private float workerStandOff = 0.3f;
        [Tooltip("Угол между рабочими у здания, градусы.")]
        [SerializeField] private float workerSpread = 35f;
        [Tooltip("Насколько рабочий «стучит молотком» — качается взад-вперёд, u.")]
        [SerializeField] private float workerBob = 0.08f;
        [SerializeField] private float workerBobRate = 9f;

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

        public float RecruitWalkSpeed => recruitWalkSpeed;

        public Vector2 RecruitsSpacing => recruitsSpacing;

        public int RecruitsPerRow => recruitsPerRow;

        public float RecruitsGroupGap => recruitsGroupGap;

        public Sprite BarracksRoofSprite => barracksRoofSprite;

        public Vector2[] BarracksDoors => barracksDoors;

        public Vector2 BarracksGate => barracksGate;

        public float GatePassDepth => gatePassDepth;

        public Sprite CircleSprite => circleSprite;

        public Vector2 HeadquartersSize => headquartersSize;

        public Vector2 BarracksSize => barracksSize;

        public float HeadquartersInnerScale => headquartersInnerScale;

        public Color InnerColor => innerColor;

        public float ConstructionAlpha => constructionAlpha;

        public Sprite AnnexBaseSprite => annexBaseSprite;

        public Sprite AnnexRoofSprite => annexRoofSprite;

        public Vector2 AnnexSize => annexSize;

        public Vector2[] AnnexOffsets => annexOffsets;

        public Vector2 ProgressBarSize => progressBarSize;

        public float ProgressBarOffset => progressBarOffset;

        public Color ProgressBackground => progressBackground;

        public Color ProgressColor => progressColor;

        public Color HealthColor => healthColor;

        public Color SelectionColor => selectionColor;

        public float SelectionPadding => selectionPadding;

        public Color BuildRadiusColor => buildRadiusColor;

        public Color ValidSiteColor => validSiteColor;

        public Color InvalidSiteColor => invalidSiteColor;

        public Color DamagedTint => damagedTint;

        public Vector2[] DamageSpots => damageSpots;

        public float SmokeHealth => smokeHealth;

        public float FireHealth => fireHealth;

        public Sprite SmokeSprite => smokeSprite;

        public Color SmokeColor => smokeColor;

        public Vector2 SmokeSize => smokeSize;

        public float SmokeRise => smokeRise;

        public float SmokePeriod => smokePeriod;

        public Color FireColor => fireColor;

        public float FireSize => fireSize;

        public Color HoleColor => holeColor;

        public float HoleSize => holeSize;

        public Color SentryRangeColor => sentryRangeColor;

        public Vector2[] SentryPosts => sentryPosts;

        public float SentryAlertTime => sentryAlertTime;

        public float SentryScale => sentryScale;

        public Color WorkerColor => workerColor;

        public int WorkersPerBuilding => workersPerBuilding;

        public float WorkerSpeed => workerSpeed;

        public float WorkerStandOff => workerStandOff;

        public float WorkerSpread => workerSpread;

        public float WorkerBob => workerBob;

        public float WorkerBobRate => workerBobRate;

        /// <summary>Габариты здания этого типа.</summary>
        public Vector2 SizeOf(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Headquarters:
                    return headquartersSize;
                case BuildingType.Stable:
                    return stableSize;
                default:
                    return barracksSize;
            }
        }

        public int BodySortingOrder => bodySortingOrder;

        public int OverlaySortingOrder => overlaySortingOrder;
    }
}
