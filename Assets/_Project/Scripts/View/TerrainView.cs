using System.Collections.Generic;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Terrain;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Marchlands.View
{
    /// <summary>
    /// Местность полной карты: реки слева и справа, сплошной непроходимый лес сверху и снизу, грунтовые дороги,
    /// рощи и кусты с травой на поле.
    /// Рисуется один раз при старте — местность в бою не меняется. Без местности ничего не рисует.
    /// </summary>
    public sealed class TerrainView : MonoBehaviour
    {
        // Золотой угол для раскладки деревьев «подсолнухом»: роща выглядит плотно и без рядов.
        private const float GoldenAngle = 2.39996323f;

        [SerializeField] private Sprite squareSprite;
        [SerializeField] private Sprite discSprite;
        [Tooltip("Кольцо — горизонтали холмов.")]
        [SerializeField] private Sprite circleSprite;
        [Tooltip("Цельные текстуры рек; растягиваются на полосу между полем и краем карты.")]
        [SerializeField] private Sprite westRiverSprite;
        [SerializeField] private Sprite eastRiverSprite;
        [Tooltip("Течение: маски воды рек (по форме русла) и тайловые блики, плывущие на север.")]
        [SerializeField] private Sprite westRiverWaterMask;
        [SerializeField] private Sprite eastRiverWaterMask;
        [SerializeField] private Sprite riverFlowSprite;
        [SerializeField] private float riverFlowSpeed = 0.8f;

        [Header("За краем карты")]
        [Tooltip("Насколько реки выходят за край карты вверх и вниз, u: текут сквозь лес до края видимого.")]
        [FormerlySerializedAs("mountainExtension")]
        [SerializeField] private float riverExtension = 8f;
        [Tooltip("Насколько за край карты тянется лес, u — дальше, чем может показать камера.")]
        [SerializeField] private float outerExtent = 40f;

        [Header("Лес сверху и снизу (непроходимая граница)")]
        [Tooltip("Тайловый полог сплошного леса (вид сверху).")]
        [SerializeField] private Sprite borderCanopySprite;
        [Tooltip("Сколько юнитов занимает один повтор полога.")]
        [SerializeField] private float borderCanopyTile = 7f;
        [SerializeField] private Color borderCanopyColor = new Color(0.82f, 0.9f, 0.8f);
        [Tooltip("Глубина опушки из отдельных деревьев от края поля, u; дальше — сплошной полог.")]
        [SerializeField] private float borderEdgeDepth = 8f;
        [Tooltip("Насколько неровная опушка: на сколько юнитов деревья выступают к полю и отступают.")]
        [SerializeField] private float borderEdgeJitter = 2f;
        [Tooltip("Шаг сетки деревьев опушки, u.")]
        [SerializeField] private float borderTreeSpacing = 1.3f;
        [SerializeField] private Vector2 borderTreeSize = new Vector2(2f, 3.4f);
        [Tooltip("Деревья опушки темнеют вглубь леса до этого оттенка — переходят в сплошной полог без шва.")]
        [SerializeField] private Color borderTreeShade = new Color(0.55f, 0.66f, 0.55f);

        [Header("Размер карты (как у фона)")]
        [SerializeField] private Vector2 mapSize = new Vector2(120f, 80f);

        [Header("Дороги")]
        [SerializeField] private float roadWidth = 2.6f;
        [SerializeField] private Color roadColor = new Color(0.66f, 0.56f, 0.38f);
        [Tooltip("Материал дороги с тайловой текстурой грунтовки (колеи, края); без него дорога — ровная полоса roadColor.")]
        [SerializeField] private Material roadMaterial;
        [Tooltip("Сколько юнитов дороги занимает один повтор текстуры вдоль неё.")]
        [SerializeField] private float roadTextureLength = 5f;
        [Tooltip("Поля текстуры за краем дороги, где на неё заползает трава, u с каждой стороны.")]
        [SerializeField] private float roadFringe = 0.4f;
        [Tooltip("Шаг сглаживания линии дороги, u.")]
        [SerializeField] private float roadSmoothStep = 0.4f;
        [Tooltip("Насколько гуляет ширина дороги, доля.")]
        [SerializeField] private float roadWidthJitter = 0.04f;
        [Tooltip("На какой длине дорога растворяется у концов (на развилках), u.")]
        [SerializeField] private float roadEndFade = 1.2f;

        [Header("Рощи")]
        [Tooltip("Виды деревьев (вид сверху, с тенью); в каждой роще один вид преобладает.")]
        [SerializeField] private Sprite[] treeSprites;
        [Tooltip("Какая доля деревьев рощи — её преобладающего вида.")]
        [SerializeField, Range(0f, 1f)] private float dominantTreeShare = 0.5f;
        [SerializeField] private Vector2 treeSize = new Vector2(1.6f, 3f);
        [Tooltip("Насколько деревья различаются оттенком: крона темнеет или желтеет до этого цвета.")]
        [SerializeField] private Color[] treeTints = { new Color(0.82f, 0.9f, 0.78f), new Color(1f, 0.97f, 0.82f) };
        [Tooltip("Сколько деревьев на квадратную единицу рощи.")]
        [SerializeField] private float treeDensity = 0.35f;
        [Tooltip("Подлесок: мягкие пятна тёмной травы под деревьями, без чёткой границы.")]
        [SerializeField] private Sprite undergrowthSprite;
        [SerializeField] private Color undergrowthColor = new Color(0.2f, 0.32f, 0.14f, 0.45f);
        [Tooltip("Сколько пятен подлеска на единицу радиуса рощи.")]
        [SerializeField] private float undergrowthPerRadius = 2f;
        [Tooltip("Размер пятна подлеска, доля радиуса рощи: от и до.")]
        [SerializeField] private Vector2 undergrowthSize = new Vector2(0.7f, 1.2f);
        [Tooltip("Сколько кустов по опушке на единицу радиуса рощи.")]
        [SerializeField] private float edgeBushesPerRadius = 1.2f;

        [Header("Река через поле, переправы, холмы")]
        [Tooltip("Текстура реки через поле (как у боковых: вода, песчаные берега, трава по краям); вдоль — повторяется с отражением.")]
        [SerializeField] private Texture2D fieldRiverTexture;
        [Tooltip("Сколько текселей текстуры поперёк занимает вода — по нему текстура масштабируется под ширину реки.")]
        [SerializeField] private float fieldRiverWaterTexels = 60f;
        [Tooltip("Запасной вид, если текстуры нет: цвета полос реки.")]
        [SerializeField] private Color riverWaterColor = new Color(0.27f, 0.46f, 0.66f);
        [SerializeField] private Color riverDeepColor = new Color(0.2f, 0.37f, 0.57f);
        [SerializeField] private Color riverBankColor = new Color(0.62f, 0.55f, 0.38f);
        [SerializeField] private Color riverReedColor = new Color(0.27f, 0.4f, 0.18f, 0.55f);
        [Tooltip("Ширина берега (песок, грязь) и полосы прибрежной травы, u.")]
        [SerializeField] private float riverBankWidth = 0.7f;
        [SerializeField] private float riverReedWidth = 1.2f;
        [Tooltip("Шаг, с которым река рисуется кругами и прямоугольниками, u.")]
        [SerializeField] private float riverStep = 0.7f;
        [SerializeField] private Color fordWaterColor = new Color(0.45f, 0.6f, 0.72f);
        [SerializeField] private Color fordStoneColor = new Color(0.62f, 0.6f, 0.55f);
        [SerializeField] private Color bridgeColor = new Color(0.5f, 0.36f, 0.22f);
        [SerializeField] private Color bridgeRailColor = new Color(0.32f, 0.22f, 0.13f);
        [Tooltip("Ширина моста, u.")]
        [SerializeField] private float bridgeWidth = 3.2f;
        [Tooltip("Холм: освещённый склон, тень и горизонтали.")]
        [SerializeField] private Color hillLightColor = new Color(0.86f, 0.84f, 0.5f, 0.22f);
        [SerializeField] private Color hillShadowColor = new Color(0.12f, 0.2f, 0.06f, 0.2f);
        [SerializeField] private Color hillContourColor = new Color(0.36f, 0.3f, 0.16f, 0.22f);
        [SerializeField] private int hillContours = 3;

        [Header("Убранство поля (только вид)")]
        [Tooltip("Кусты — рисуются поверх травы, но под деревнями, зданиями и отрядами.")]
        [SerializeField] private Sprite[] bushSprites;
        [Tooltip("Плоская мелочь: пучки травы, цветы, камешки.")]
        [SerializeField] private Sprite[] tuftSprites;
        [Tooltip("Шаг сетки, по которой разбросано убранство, u.")]
        [SerializeField] private float decorSpacing = 2f;
        [SerializeField, Range(0f, 1f)] private float bushChance = 0.12f;
        [SerializeField, Range(0f, 1f)] private float tuftChance = 0.5f;
        [SerializeField] private Vector2 bushSize = new Vector2(1.5f, 2.6f);
        [SerializeField] private Vector2 tuftSize = new Vector2(0.9f, 1.5f);
        [Tooltip("Насколько убранство держится от дорог и опушек, u.")]
        [SerializeField] private float decorClearance = 0.8f;
        [Tooltip("Насколько убранство держится от центров деревень и штабов, u.")]
        [SerializeField] private float settlementClearance = 6f;
        [Tooltip("Мягкие пятна светлой и тёмной травы — поле не выглядит залитым одним цветом.")]
        [SerializeField] private Sprite patchSprite;
        [SerializeField] private Color[] patchColors = { new Color(0.75f, 0.8f, 0.35f, 0.16f), new Color(0.15f, 0.3f, 0.1f, 0.14f) };
        [SerializeField] private float patchSpacing = 9f;
        [SerializeField] private Vector2 patchSize = new Vector2(7f, 14f);

        [Header("Порядок отрисовки")]
        [SerializeField] private int roadSortingOrder = -80;
        [SerializeField] private int forestFloorSortingOrder = -75;
        [SerializeField] private int borderSortingOrder = -60;
        [SerializeField] private int treeSortingOrder = -45;
        [SerializeField] private int patchSortingOrder = -85;
        [SerializeField] private int tuftSortingOrder = -72;
        [SerializeField] private int bushSortingOrder = -55;

        /// <param name="size">Размер карты из сценария; не задан — как в настройках сцены.</param>
        public void Initialize(Match match, Vector2? size = null)
        {
            if (size.HasValue)
            {
                mapSize = size.Value;
            }

            TerrainMap terrain = match.Terrain;
            if (terrain == null)
            {
                return;
            }

            DrawOuterForest(terrain);
            DrawRivers(terrain);
            DrawBorderForest(terrain);
            DrawHills(terrain);
            DrawFieldRivers(terrain);
            DrawRoads(terrain);
            DrawForests(terrain);
            DrawDecor(match);
        }

        /// <summary>
        /// Кусты, пучки травы, цветы и камешки на пустом поле: по сетке со случайным сдвигом, в обход дорог, леса,
        /// деревень и штабов. Раскладка одинакова при каждом запуске.
        /// </summary>
        private void DrawDecor(Match match)
        {
            TerrainMap terrain = match.Terrain;
            if (decorSpacing <= 0f || ((bushSprites == null || bushSprites.Length == 0) && (tuftSprites == null || tuftSprites.Length == 0)))
            {
                return;
            }

            var settlements = new List<float2>();
            foreach (Village village in match.Villages)
            {
                settlements.Add(village.Position);
            }

            foreach (Building building in match.Buildings)
            {
                settlements.Add(building.Position);
            }

            float halfX = mapSize.x * 0.5f, halfY = mapSize.y * 0.5f;
            int seed = 7919;
            if (patchSprite != null && patchColors != null && patchColors.Length > 0 && patchSpacing > 0f)
            {
                for (float y = -halfY; y < halfY; y += patchSpacing)
                {
                    for (float x = -halfX; x < halfX; x += patchSpacing)
                    {
                        float2 jitter = new float2(Hash01(++seed), Hash01(++seed));
                        float2 position = new float2(x, y) + jitter * patchSpacing;
                        Color color = patchColors[math.min(patchColors.Length - 1, (int)(Hash01(++seed) * patchColors.Length))];
                        float size = math.lerp(patchSize.x, patchSize.y, Hash01(++seed));
                        CreateStretched("Grass Patch", patchSprite, patchSortingOrder, position, new Vector2(size, size * 0.7f), Hash01(++seed) * 180f, color);
                    }
                }
            }

            for (float y = -halfY; y < halfY; y += decorSpacing)
            {
                for (float x = -halfX; x < halfX; x += decorSpacing)
                {
                    float pick = Hash01(++seed);
                    float2 jitter = new float2(Hash01(++seed), Hash01(++seed)) - 0.5f;
                    float2 position = new float2(x, y) + (0.5f + jitter * 0.9f) * decorSpacing;
                    float variant = Hash01(++seed);
                    bool bush = pick < bushChance && bushSprites != null && bushSprites.Length > 0;
                    bool tuft = !bush && pick < bushChance + tuftChance && tuftSprites != null && tuftSprites.Length > 0;
                    if ((!bush && !tuft) || !IsOpenGround(terrain, settlements, position))
                    {
                        continue;
                    }

                    Sprite[] sprites = bush ? bushSprites : tuftSprites;
                    Sprite sprite = sprites[math.min(sprites.Length - 1, (int)(variant * sprites.Length))];
                    Vector2 range = bush ? bushSize : tuftSize;
                    float size = math.lerp(range.x, range.y, Hash01(++seed));
                    // У кустов — та же перспектива, что у деревьев: нижние поверх верхних.
                    int order = bush ? bushSortingOrder + (int)((halfY - position.y) * 0.05f) : tuftSortingOrder;
                    CreateStretched(bush ? "Bush" : "Tuft", sprite, order, position, new Vector2(size, size), bush ? 0f : Hash01(++seed) * 360f, Color.white);
                }
            }
        }

        private bool IsOpenGround(TerrainMap terrain, List<float2> settlements, float2 position)
        {
            if (!terrain.Contains(position) || terrain.IsOnRoad(position, roadWidth * 0.5f + decorClearance)
                || terrain.DistanceToRivers(position) < riverBankWidth + riverReedWidth + decorClearance)
            {
                return false;
            }

            foreach (Forest forest in terrain.Forests)
            {
                if (math.distance(forest.Center, position) < forest.Radius + decorClearance)
                {
                    return false;
                }
            }

            foreach (float2 settlement in settlements)
            {
                if (math.distance(settlement, position) < settlementClearance)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Реки слева и справа: полоса от края поля до края карты, по всей высоте — сквозь лес сверху и снизу.</summary>
        private void DrawRivers(TerrainMap terrain)
        {
            float halfMapX = mapSize.x * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                float edge = side > 0f ? terrain.BoundsMax.x : terrain.BoundsMin.x;
                float width = halfMapX - math.abs(edge);
                if (width > 0f)
                {
                    Sprite sprite = side > 0f ? eastRiverSprite : westRiverSprite;
                    // Река идёт до самого края видимого — сквозь лес (поверх его полога).
                    var center = new Vector2(edge + side * width * 0.5f, 0f);
                    var size = new Vector2(width, mapSize.y + riverExtension * 2f);
                    CreateStretched("River", sprite, borderSortingOrder + 4, center, size);
                    DrawRiverFlow(side > 0f ? eastRiverWaterMask : westRiverWaterMask, center, size);
                }
            }
        }

        /// <summary>Блики по воде, плывущие на север; маска по форме русла не пускает их на берега.</summary>
        private void DrawRiverFlow(Sprite waterMask, Vector2 center, Vector2 size)
        {
            if (waterMask == null || riverFlowSprite == null)
            {
                return;
            }

            var maskObject = new GameObject("River Water Mask");
            maskObject.transform.SetParent(transform, false);
            maskObject.transform.position = center;
            Vector2 maskSize = waterMask.bounds.size;
            maskObject.transform.localScale = new Vector3(size.x / maskSize.x, size.y / maskSize.y, 1f);
            var mask = maskObject.AddComponent<SpriteMask>();
            // Маска действует только на блики (у них VisibleInsideMask); свой диапазон сортировки ломает отрисовку.
            mask.sprite = waterMask;

            float period = riverFlowSprite.bounds.size.y;
            var flow = new GameObject("River Flow");
            flow.transform.SetParent(transform, false);
            flow.transform.position = center - new Vector2(0f, period * 0.5f);
            var flowRenderer = flow.AddComponent<SpriteRenderer>();
            flowRenderer.sprite = riverFlowSprite;
            flowRenderer.drawMode = SpriteDrawMode.Tiled;
            flowRenderer.size = new Vector2(size.x, size.y + period);
            flowRenderer.sortingOrder = borderSortingOrder + 5;
            flowRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            flow.AddComponent<RiverFlow>().Initialize(riverFlowSpeed, period);
        }

        /// <summary>
        /// Сверху и снизу — явно непроходимый сплошной лес, уходящий за край видимого: у поля неровная опушка из отдельных
        /// деревьев шести видов (с подлеском и кустами), дальше — сплошной полог. Реки текут сквозь него поверх полога.
        /// </summary>
        private void DrawBorderForest(TerrainMap terrain)
        {
            float halfMapX = mapSize.x * 0.5f;
            float halfMapY = mapSize.y * 0.5f;
            int seed = 50021;
            foreach (float side in new[] { -1f, 1f })
            {
                float edge = side > 0f ? terrain.BoundsMax.y : terrain.BoundsMin.y;
                float outer = side * (halfMapY + outerExtent);

                // Полог — от середины опушки до края видимого и шире карты: под деревьями опушки не просвечивает трава.
                if (borderCanopySprite != null)
                {
                    float start = edge + side * borderEdgeDepth * 0.4f;
                    float depth = math.abs(outer - start);
                    // Между реками: дальше лес за реками со своим пологом, стык прячется под водой.
                    float width = terrain.BoundsMax.x - terrain.BoundsMin.x;
                    CreateTiled("Border Canopy", borderCanopySprite, borderSortingOrder + 2,
                        new Vector2((terrain.BoundsMin.x + terrain.BoundsMax.x) * 0.5f, (start + outer) * 0.5f), new Vector2(width, depth),
                        borderCanopyTile, borderCanopyColor);
                }

                if (treeSprites == null || treeSprites.Length == 0 || borderTreeSpacing <= 0f)
                {
                    continue;
                }

                // Опушка — только между реками: за реками лес свой.
                float minX = -halfMapX - borderTreeSpacing, maxX = halfMapX + borderTreeSpacing;
                float riverLeft = terrain.BoundsMin.x, riverRight = terrain.BoundsMax.x;
                for (float x = minX; x <= maxX; x += borderTreeSpacing)
                {
                    // Неровный край: опушка то выступает к полю, то отступает.
                    float wave = Mathf.PerlinNoise(x * 0.12f, side * 17.3f + 3.1f) * 2f - 1f;
                    float edgeLine = edge + side * (borderEdgeJitter * (0.5f + 0.5f * wave));
                    float refY = side > 0f ? edge + borderEdgeDepth + borderEdgeJitter : edgeLine;
                    for (float d = 0f; d <= borderEdgeDepth; d += borderTreeSpacing)
                    {
                        float2 jitter = new float2(Hash01(++seed), Hash01(++seed)) - 0.5f;
                        float2 position = new float2(x + jitter.x * borderTreeSpacing, edgeLine + side * (d + jitter.y * borderTreeSpacing * 0.8f));
                        if (position.x < riverLeft - 0.5f || position.x > riverRight + 0.5f)
                        {
                            continue;
                        }

                        float size = math.lerp(borderTreeSize.x, borderTreeSize.y, Hash01(++seed));
                        Sprite sprite = treeSprites[(int)(Hash01(++seed) * treeSprites.Length) % treeSprites.Length];
                        Color tint = treeTints != null && treeTints.Length > 0
                            ? Color.Lerp(Color.white, treeTints[(int)(Hash01(++seed) * treeTints.Length) % treeTints.Length], Hash01(++seed))
                            : Color.white;
                        // Чем глубже в лес, тем темнее — у самой опушки деревья тоже чуть притенены: лес сплошной и тёмный.
                        tint *= Color.Lerp(Color.Lerp(Color.white, borderTreeShade, 0.35f), borderTreeShade, d / borderEdgeDepth);
                        tint.a = 1f;
                        // Нижние деревья поверх верхних, как в рощах.
                        int order = treeSortingOrder + math.max(0, (int)((refY - position.y) * 2f));
                        CreateStretched("Border Tree", sprite, order, position, new Vector2(size, size), 0f, tint);
                    }

                    // У самой опушки — тёмный подлесок и кусты: край размыт.
                    float2 rim = new float2(x, edgeLine - side * 0.3f);
                    if (undergrowthSprite != null)
                    {
                        float patch = borderTreeSpacing * 2.2f;
                        CreateStretched("Border Undergrowth", undergrowthSprite, forestFloorSortingOrder, rim + new float2(0f, side * 0.8f),
                            new Vector2(patch, patch * 0.8f), Hash01(++seed) * 180f, undergrowthColor);
                    }

                    if (bushSprites != null && bushSprites.Length > 0 && Hash01(++seed) < 0.45f
                        && rim.x > riverLeft + 1f && rim.x < riverRight - 1f)
                    {
                        float bush = math.lerp(bushSize.x, bushSize.y, Hash01(++seed));
                        Sprite sprite = bushSprites[(int)(Hash01(++seed) * bushSprites.Length) % bushSprites.Length];
                        CreateStretched("Border Bush", sprite, bushSortingOrder, rim - new float2(0f, side * Hash01(++seed) * 0.6f), new Vector2(bush, bush), 0f, Color.white);
                    }
                }
            }
        }

        /// <summary>
        /// Река через поле: полоса прибрежной травы, берег, вода и тёмная стремнина посередине — кругами и прямоугольниками
        /// по сглаженной оси русла. Поверх — переправы: брод (светлая мель с камнями) и деревянный мост с перилами.
        /// </summary>
        private void DrawFieldRivers(TerrainMap terrain)
        {
            foreach (River river in terrain.Rivers)
            {
                var points = new List<Vector2>(river.Points.Length);
                foreach (float2 point in river.Points)
                {
                    points.Add(new Vector2(point.x, point.y));
                }

                List<Vector2> line = RoadShape.Smooth(points, riverStep);
                float hw = river.HalfWidth;
                if (fieldRiverTexture != null && roadMaterial != null)
                {
                    DrawRiverMesh(line, hw);
                    continue;
                }

                DrawRibbon(line, (hw + riverBankWidth + riverReedWidth) * 2f, riverReedColor, borderSortingOrder - 6);
                DrawRibbon(line, (hw + riverBankWidth) * 2f, riverBankColor, borderSortingOrder - 5);
                DrawRibbon(line, hw * 2f, riverWaterColor, borderSortingOrder - 4);
                DrawRibbon(line, hw * 0.9f, riverDeepColor, borderSortingOrder - 3);
            }

            int seed = 90001;
            foreach (Crossing crossing in terrain.Crossings)
            {
                float2 across = crossing.Across;
                float length = math.distance(crossing.BankA, crossing.BankB);
                float angle = math.degrees(math.atan2(across.y, across.x));
                if (crossing.Type == CrossingType.Ford)
                {
                    // Мель: светлая вода в пределах русла и россыпь камней поперёк реки.
                    float water = math.max(1f, length - 3f);
                    CreateStretched("Ford", discSprite, borderSortingOrder - 2, (Vector2)crossing.Center,
                        new Vector2(water * 0.95f, crossing.Radius * 1.1f), angle, fordWaterColor);
                    for (int i = 0; i < 26; i++)
                    {
                        float t = (Hash01(++seed) - 0.5f) * 0.85f;
                        float side = (Hash01(++seed) - 0.5f) * crossing.Radius * 0.8f;
                        float2 along = new float2(-across.y, across.x);
                        float2 stone = crossing.Center + across * (t * water) + along * side;
                        float size = math.lerp(0.25f, 0.6f, Hash01(++seed));
                        CreateStretched("Ford Stone", discSprite, borderSortingOrder - 1, (Vector2)stone,
                            new Vector2(size, size * 0.8f), Hash01(++seed) * 180f, fordStoneColor * math.lerp(0.85f, 1.1f, Hash01(++seed)));
                    }
                }
                else
                {
                    // Мост: настил вдоль дороги, что через него идёт (иначе — поперёк реки), доски и перила.
                    if (TryRoadDirection(terrain, crossing.Center, crossing.Radius * 1.5f, out float2 roadDirection))
                    {
                        across = math.dot(roadDirection, across) < 0f ? -roadDirection : roadDirection;
                        angle = math.degrees(math.atan2(across.y, across.x));
                    }

                    float deck = length + 1.2f;
                    Create("Bridge", squareSprite, bridgeColor, borderSortingOrder - 2, (Vector2)crossing.Center, new Vector2(deck, bridgeWidth), angle);
                    float2 along = new float2(-across.y, across.x);
                    int planks = (int)(deck / 0.45f);
                    for (int i = 0; i < planks; i++)
                    {
                        float2 at = crossing.Center + across * ((i + 0.5f) / planks - 0.5f) * deck;
                        Create("Plank", squareSprite, bridgeColor * 0.8f, borderSortingOrder - 1, (Vector2)at, new Vector2(0.06f, bridgeWidth * 0.96f), angle);
                    }

                    foreach (float sign in new[] { -1f, 1f })
                    {
                        float2 rail = crossing.Center + along * (sign * (bridgeWidth * 0.5f - 0.1f));
                        Create("Rail", squareSprite, bridgeRailColor, borderSortingOrder, (Vector2)rail, new Vector2(deck, 0.22f), angle);
                    }
                }
            }
        }

        /// <summary>
        /// Река текстурой, как боковые: лента по сглаженной оси русла, вода шириной 2 × <paramref name="halfWidth"/>,
        /// неровные песчаные берега и трава по краям, растворяющаяся в поле. Текстура отражается по длине — без швов.
        /// </summary>
        private void DrawRiverMesh(List<Vector2> line, float halfWidth)
        {
            float texelsPerUnit = fieldRiverWaterTexels / math.max(0.01f, halfWidth * 2f);
            float width = fieldRiverTexture.width / texelsPerUnit;
            float tileLength = fieldRiverTexture.height / texelsPerUnit;

            var lengths = new float[line.Count];
            for (int i = 1; i < line.Count; i++)
            {
                lengths[i] = lengths[i - 1] + Vector2.Distance(line[i - 1], line[i]);
            }

            var vertices = new Vector3[line.Count * 2];
            var uvs = new Vector2[line.Count * 2];
            var colors = new Color[line.Count * 2];
            var triangles = new int[(line.Count - 1) * 6];
            for (int i = 0; i < line.Count; i++)
            {
                Vector2 tangent = (line[math.min(i + 1, line.Count - 1)] - line[math.max(i - 1, 0)]).normalized;
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                vertices[i * 2] = line[i] + normal * (width * 0.5f);
                vertices[i * 2 + 1] = line[i] - normal * (width * 0.5f);
                float v = lengths[i] / tileLength;
                uvs[i * 2] = new Vector2(0f, v);
                uvs[i * 2 + 1] = new Vector2(1f, v);
                colors[i * 2] = colors[i * 2 + 1] = Color.white;
                if (i + 1 < line.Count)
                {
                    int t = i * 6;
                    triangles[t] = i * 2;
                    triangles[t + 1] = i * 2 + 2;
                    triangles[t + 2] = i * 2 + 1;
                    triangles[t + 3] = i * 2 + 1;
                    triangles[t + 4] = i * 2 + 2;
                    triangles[t + 5] = i * 2 + 3;
                }
            }

            var mesh = new Mesh { name = "Field River" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            var material = new Material(roadMaterial) { mainTexture = fieldRiverTexture };
            var child = new GameObject("Field River");
            child.transform.SetParent(transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = child.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.sortingOrder = borderSortingOrder - 4;
        }

        /// <summary>Направление дороги, ближайшей к точке (не дальше <paramref name="radius"/>); false — дорог рядом нет.</summary>
        private static bool TryRoadDirection(TerrainMap terrain, float2 point, float radius, out float2 direction)
        {
            direction = float2.zero;
            float best = radius;
            foreach (float2[] road in terrain.Roads)
            {
                for (int i = 0; i + 1 < road.Length; i++)
                {
                    float2 ab = road[i + 1] - road[i];
                    float lengthSq = math.lengthsq(ab);
                    float t = lengthSq > 0f ? math.saturate(math.dot(point - road[i], ab) / lengthSq) : 0f;
                    float distance = math.distance(point, road[i] + ab * t);
                    if (distance <= best && lengthSq > 0f)
                    {
                        best = distance;
                        direction = math.normalize(ab);
                    }
                }
            }

            return math.lengthsq(direction) > 0f;
        }

        /// <summary>Лента из кругов и прямоугольников вдоль линии: ровные края и скруглённые изгибы.</summary>
        private void DrawRibbon(List<Vector2> line, float width, Color color, int order)
        {
            for (int i = 0; i < line.Count; i++)
            {
                Create("River Part", discSprite, color, order, line[i], new Vector2(width, width), 0f);
                if (i + 1 >= line.Count)
                {
                    continue;
                }

                Vector2 delta = line[i + 1] - line[i];
                float angle = math.degrees(math.atan2(delta.y, delta.x));
                Create("River Part", squareSprite, color, order, (line[i] + line[i + 1]) * 0.5f, new Vector2(delta.magnitude, width), angle);
            }
        }

        /// <summary>
        /// Пологий холм: освещённый склон к северо-западу, тень к юго-востоку и несколько горизонталей —
        /// без чёткой границы, как рощи. Механика действует в радиусе холма.
        /// </summary>
        private void DrawHills(TerrainMap terrain)
        {
            Sprite soft = patchSprite != null ? patchSprite : discSprite;
            foreach (Hill hill in terrain.Hills)
            {
                float r = hill.Radius;
                CreateStretched("Hill Shadow", soft, patchSortingOrder + 1, (Vector2)(hill.Center + new float2(r * 0.25f, -r * 0.25f)),
                    new Vector2(r * 2.3f, r * 2.1f), 0f, hillShadowColor);
                CreateStretched("Hill Light", soft, patchSortingOrder + 2, (Vector2)(hill.Center + new float2(-r * 0.2f, r * 0.2f)),
                    new Vector2(r * 1.9f, r * 1.8f), 0f, hillLightColor);
                if (circleSprite == null)
                {
                    continue;
                }

                for (int i = 0; i < hillContours; i++)
                {
                    float scale = 1f - i / (float)math.max(1, hillContours);
                    float2 offset = new float2(-r * 0.08f, r * 0.08f) * i;
                    CreateStretched("Hill Contour", circleSprite, patchSortingOrder + 3, (Vector2)(hill.Center + offset),
                        new Vector2(r * 2f * scale, r * 1.85f * scale), 0f, hillContourColor);
                }
            }
        }

        /// <summary>Повторяющаяся текстура на прямоугольник: один повтор — <paramref name="tile"/> юнитов.</summary>
        private void CreateTiled(string objectName, Sprite sprite, int sortingOrder, Vector2 position, Vector2 size, float tile, Color color)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            child.transform.position = position;
            Vector2 spriteSize = sprite.bounds.size;
            float scale = tile / math.max(spriteSize.x, 1e-3f);
            child.transform.localScale = new Vector3(scale, scale, 1f);
            var spriteRenderer = child.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.drawMode = SpriteDrawMode.Tiled;
            spriteRenderer.size = size / scale;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = sortingOrder;
        }

        /// <summary>
        /// За реками слева и справа — тот же сплошной лес, что сверху и снизу: полог от края поля (под рекой) до края
        /// видимого и по всей высоте, а у дальнего берега — неровная опушка из отдельных деревьев шести видов,
        /// кроны чуть нависают над берегом; вглубь деревья темнеют и сливаются с пологом.
        /// </summary>
        private void DrawOuterForest(TerrainMap terrain)
        {
            float halfMapX = mapSize.x * 0.5f;
            float halfMapY = mapSize.y * 0.5f;
            int seed = 70117;
            foreach (float side in new[] { -1f, 1f })
            {
                float edge = side > 0f ? terrain.BoundsMax.x : terrain.BoundsMin.x;
                float outer = side * (halfMapX + outerExtent);
                if (borderCanopySprite != null)
                {
                    CreateTiled("Outer Canopy", borderCanopySprite, borderSortingOrder + 2,
                        new Vector2((edge + outer) * 0.5f, 0f), new Vector2(math.abs(outer - edge), (halfMapY + outerExtent) * 2f),
                        borderCanopyTile, borderCanopyColor);
                }

                if (treeSprites == null || treeSprites.Length == 0 || borderTreeSpacing <= 0f)
                {
                    continue;
                }

                float bank = side * halfMapX;
                float top = halfMapY + borderEdgeDepth;
                for (float y = -top; y <= top; y += borderTreeSpacing)
                {
                    float wave = Mathf.PerlinNoise(y * 0.15f, side * 23.7f + 9.2f);
                    float edgeLine = bank + side * (borderEdgeJitter * 0.6f * wave - 0.3f);
                    for (float d = 0f; d <= borderEdgeDepth; d += borderTreeSpacing)
                    {
                        float2 jitter = new float2(Hash01(++seed), Hash01(++seed)) - 0.5f;
                        float2 position = new float2(edgeLine + side * (d + jitter.x * borderTreeSpacing * 0.8f), y + jitter.y * borderTreeSpacing);
                        float size = math.lerp(borderTreeSize.x, borderTreeSize.y, Hash01(++seed));
                        Sprite sprite = treeSprites[(int)(Hash01(++seed) * treeSprites.Length) % treeSprites.Length];
                        Color tint = treeTints != null && treeTints.Length > 0
                            ? Color.Lerp(Color.white, treeTints[(int)(Hash01(++seed) * treeTints.Length) % treeTints.Length], Hash01(++seed))
                            : Color.white;
                        tint *= Color.Lerp(Color.Lerp(Color.white, borderTreeShade, 0.35f), borderTreeShade, d / borderEdgeDepth);
                        tint.a = 1f;
                        // Нижние кроны поверх верхних; шаг порядка крупный — лес за рекой не встаёт над отрядами.
                        int order = treeSortingOrder + math.max(0, (int)((top - position.y) * 0.5f));
                        CreateStretched("Outer Tree", sprite, order, position, new Vector2(size, size), 0f, tint);
                    }
                }
            }
        }

        private void DrawRoads(TerrainMap terrain)
        {
            if (roadMaterial != null)
            {
                int index = 0;
                foreach (float2[] road in terrain.Roads)
                {
                    DrawRoadMesh(road, index++);
                }

                return;
            }

            foreach (float2[] road in terrain.Roads)
            {
                for (int i = 0; i < road.Length; i++)
                {
                    Create("Road Joint", discSprite, roadColor, roadSortingOrder, road[i], new Vector2(roadWidth, roadWidth), 0f);
                    if (i + 1 >= road.Length)
                    {
                        continue;
                    }

                    float2 delta = road[i + 1] - road[i];
                    float length = math.length(delta);
                    float angle = math.degrees(math.atan2(delta.y, delta.x));
                    Create("Road", squareSprite, roadColor, roadSortingOrder, (road[i] + road[i + 1]) * 0.5f, new Vector2(length, roadWidth), angle);
                }
            }
        }

        /// <summary>
        /// Дорога лентой по сглаженной линии: текстура грунтовки повторяется вдоль, ширина чуть гуляет,
        /// у концов лента растворяется — на развилках одна дорога плавно уходит в другую.
        /// </summary>
        private void DrawRoadMesh(float2[] road, int roadIndex)
        {
            var points = new List<Vector2>(road.Length);
            foreach (float2 point in road)
            {
                points.Add(new Vector2(point.x, point.y));
            }

            List<Vector2> line = RoadShape.Smooth(points, roadSmoothStep);
            if (line.Count < 2)
            {
                return;
            }

            var lengths = new float[line.Count];
            for (int i = 1; i < line.Count; i++)
            {
                lengths[i] = lengths[i - 1] + Vector2.Distance(line[i - 1], line[i]);
            }

            float total = lengths[line.Count - 1];
            var vertices = new Vector3[line.Count * 2];
            var uvs = new Vector2[line.Count * 2];
            var colors = new Color[line.Count * 2];
            var triangles = new int[(line.Count - 1) * 6];
            for (int i = 0; i < line.Count; i++)
            {
                Vector2 tangent = (line[math.min(i + 1, line.Count - 1)] - line[math.max(i - 1, 0)]).normalized;
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                float jitter = 1f + roadWidthJitter * (Mathf.PerlinNoise(lengths[i] * 0.35f, roadIndex * 7.3f) * 2f - 1f);
                float halfWidth = roadWidth * 0.5f * jitter + roadFringe;
                vertices[i * 2] = line[i] + normal * halfWidth;
                vertices[i * 2 + 1] = line[i] - normal * halfWidth;
                float v = lengths[i] / roadTextureLength;
                uvs[i * 2] = new Vector2(0f, v);
                uvs[i * 2 + 1] = new Vector2(1f, v);
                float fade = roadEndFade > 0f ? Mathf.Clamp01(math.min(lengths[i], total - lengths[i]) / roadEndFade) : 1f;
                colors[i * 2] = colors[i * 2 + 1] = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, fade));
                if (i + 1 < line.Count)
                {
                    int t = i * 6;
                    triangles[t] = i * 2;
                    triangles[t + 1] = i * 2 + 2;
                    triangles[t + 2] = i * 2 + 1;
                    triangles[t + 3] = i * 2 + 1;
                    triangles[t + 4] = i * 2 + 2;
                    triangles[t + 5] = i * 2 + 3;
                }
            }

            var mesh = new Mesh { name = "Road" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            var child = new GameObject("Road");
            child.transform.SetParent(transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = child.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = roadMaterial;
            meshRenderer.sortingOrder = roadSortingOrder + roadIndex % 2;
        }

        /// <summary>
        /// Роща: неровный подлесок из мягких пятен, кусты по опушке и деревья разных видов, размеров и оттенков.
        /// Чёткой границы нет, но замедление и укрытие действуют в радиусе рощи, как и прежде.
        /// </summary>
        private void DrawForests(TerrainMap terrain)
        {
            if (treeSprites == null || treeSprites.Length == 0)
            {
                return;
            }

            int seed = 0;
            foreach (Forest forest in terrain.Forests)
            {
                DrawUndergrowth(forest, ref seed);

                Sprite dominant = treeSprites[(int)(Hash01(++seed) * treeSprites.Length) % treeSprites.Length];
                int trees = math.max(4, (int)(math.PI * forest.Radius * forest.Radius * treeDensity));
                for (int i = 0; i < trees; i++)
                {
                    float random = Hash01(++seed);
                    // Подсолнух с лёгким разбросом — роща плотная, но без рядов и колец.
                    float distance = forest.Radius * 0.85f * math.sqrt((i + 0.5f) / trees) + (Hash01(++seed) - 0.5f) * 0.6f;
                    float angle = i * GoldenAngle + (Hash01(++seed) - 0.5f) * 0.4f;
                    float2 position = forest.Center + new float2(math.cos(angle), math.sin(angle)) * distance;
                    float size = math.lerp(treeSize.x, treeSize.y, random);
                    Sprite sprite = Hash01(++seed) < dominantTreeShare
                        ? dominant
                        : treeSprites[(int)(Hash01(++seed) * treeSprites.Length) % treeSprites.Length];
                    Color tint = treeTints != null && treeTints.Length > 0
                        ? Color.Lerp(Color.white, treeTints[(int)(Hash01(++seed) * treeTints.Length) % treeTints.Length], Hash01(++seed))
                        : Color.white;
                    // Нижние деревья рисуются поверх верхних — кроны перекрываются как в перспективе.
                    int order = treeSortingOrder + (int)((forest.Center.y + forest.Radius - position.y) * 2f);
                    CreateStretched("Tree", sprite, order, position, new Vector2(size, size), 0f, tint);
                }
            }
        }

        /// <summary>Подлесок из мягких пятен в радиусе рощи и кусты по её опушке — край размыт, а не обведён кругом.</summary>
        private void DrawUndergrowth(Forest forest, ref int seed)
        {
            if (undergrowthSprite != null)
            {
                int patches = math.max(3, (int)(forest.Radius * undergrowthPerRadius));
                for (int i = 0; i < patches; i++)
                {
                    float distance = forest.Radius * 0.55f * math.sqrt(Hash01(++seed));
                    float angle = Hash01(++seed) * math.PI * 2f;
                    float2 position = forest.Center + new float2(math.cos(angle), math.sin(angle)) * distance;
                    float size = forest.Radius * math.lerp(undergrowthSize.x, undergrowthSize.y, Hash01(++seed));
                    CreateStretched("Undergrowth", undergrowthSprite, forestFloorSortingOrder, position,
                        new Vector2(size, size * math.lerp(0.7f, 1f, Hash01(++seed))), Hash01(++seed) * 180f, undergrowthColor);
                }
            }

            if (bushSprites == null || bushSprites.Length == 0)
            {
                return;
            }

            int bushes = (int)(forest.Radius * edgeBushesPerRadius);
            for (int i = 0; i < bushes; i++)
            {
                float angle = (i + Hash01(++seed) * 0.8f) * math.PI * 2f / math.max(1, bushes);
                float distance = forest.Radius * math.lerp(0.85f, 1.05f, Hash01(++seed));
                float2 position = forest.Center + new float2(math.cos(angle), math.sin(angle)) * distance;
                float size = math.lerp(bushSize.x, bushSize.y, Hash01(++seed));
                Sprite sprite = bushSprites[(int)(Hash01(++seed) * bushSprites.Length) % bushSprites.Length];
                CreateStretched("Edge Bush", sprite, bushSortingOrder, position, new Vector2(size, size), 0f, Color.white);
            }
        }

        private void CreateStretched(string objectName, Sprite sprite, int sortingOrder, Vector2 position, Vector2 size)
        {
            CreateStretched(objectName, sprite, sortingOrder, position, size, 0f, Color.white);
        }

        private void CreateStretched(string objectName, Sprite sprite, int sortingOrder, Vector2 position, Vector2 size, float rotation, Color color)
        {
            Vector2 spriteSize = sprite.bounds.size;
            Create(objectName, sprite, color, sortingOrder, position, new Vector2(size.x / spriteSize.x, size.y / spriteSize.y), rotation);
        }

        private void Create(string objectName, Sprite sprite, Color color, int sortingOrder, Vector2 position, Vector2 size, float rotation)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            child.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, rotation));
            child.transform.localScale = new Vector3(size.x, size.y, 1f);
            var spriteRenderer = child.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = sortingOrder;
        }

        /// <summary>Детерминированное псевдослучайное число в [0, 1) — деревья и кусты стоят одинаково при каждом запуске.</summary>
        private static float Hash01(int value)
        {
            uint hash = (uint)value * 2654435761u;
            hash ^= hash >> 16;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            return (hash & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
