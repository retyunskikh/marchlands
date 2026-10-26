using System.Collections.Generic;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Terrain;
using Unity.Mathematics;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// Местность полной карты: реки слева и справа, горы сверху и снизу, грунтовые дороги, рощи и кусты с травой на поле.
    /// Рисуется один раз при старте — местность в бою не меняется. Без местности ничего не рисует.
    /// </summary>
    public sealed class TerrainView : MonoBehaviour
    {
        // Золотой угол для раскладки деревьев «подсолнухом»: роща выглядит плотно и без рядов.
        private const float GoldenAngle = 2.39996323f;

        [SerializeField] private Sprite squareSprite;
        [SerializeField] private Sprite discSprite;
        [SerializeField] private Sprite treeSprite;
        [Tooltip("Цельные текстуры гор (вид сверху) и рек; растягиваются на полосу между полем и краем карты.")]
        [SerializeField] private Sprite northMountainsSprite;
        [SerializeField] private Sprite southMountainsSprite;
        [SerializeField] private Sprite westRiverSprite;
        [SerializeField] private Sprite eastRiverSprite;

        [Header("За краем карты")]
        [Tooltip("Насколько горы выходят за край карты вверх и вниз, u: запас для перехода в туман.")]
        [SerializeField] private float mountainExtension = 8f;
        [Tooltip("Лес за реками с опушкой у реки: полотно 44 × 160 u от края поля наружу — игрок не видит пустоты.")]
        [SerializeField] private Sprite westOuterForestSprite;
        [SerializeField] private Sprite eastOuterForestSprite;
        [Tooltip("Вертикальный градиент тумана (прозрачный внизу), в который уходят горы.")]
        [SerializeField] private Sprite fogSprite;
        [SerializeField] private Color fogColor = new Color(0.78f, 0.81f, 0.84f);
        [Tooltip("Насколько за край карты тянутся лес и туман, u.")]
        [SerializeField] private float outerExtent = 40f;
        [Tooltip("За сколько до края карты начинается туман над горами, u.")]
        [SerializeField] private float fogInset = 2.5f;
        [Tooltip("Глубина перехода тумана от прозрачного к плотному, u.")]
        [SerializeField] private float fogDepth = 6f;

        [Header("Размер карты (как у фона)")]
        [SerializeField] private Vector2 mapSize = new Vector2(120f, 80f);

        [Header("Дороги")]
        [SerializeField] private float roadWidth = 2.6f;
        [SerializeField] private Color roadColor = new Color(0.66f, 0.56f, 0.38f);

        [Header("Рощи")]
        [SerializeField] private Color forestFloorColor = new Color(0.28f, 0.42f, 0.22f);
        [SerializeField] private Vector2 treeSize = new Vector2(1.8f, 2.6f);
        [Tooltip("Сколько деревьев на квадратную единицу рощи.")]
        [SerializeField] private float treeDensity = 0.35f;

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

        public void Initialize(Match match)
        {
            TerrainMap terrain = match.Terrain;
            if (terrain == null)
            {
                return;
            }

            DrawOuterForest(terrain);
            DrawRivers(terrain);
            DrawMountains(terrain);
            DrawFog();
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
            if (!terrain.Contains(position) || terrain.IsOnRoad(position, roadWidth * 0.5f + decorClearance))
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

        /// <summary>Реки слева и справа: полоса от края поля до края карты; горы сверху и снизу перекрывают их концы.</summary>
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
                    CreateStretched("River", sprite, borderSortingOrder, new Vector2(edge + side * width * 0.5f, 0f), new Vector2(width, mapSize.y));
                }
            }
        }

        /// <summary>Горы сверху и снизу: предгорья с рваным краем у поля, хребты и снежные вершины к краю карты.</summary>
        private void DrawMountains(TerrainMap terrain)
        {
            float halfMapY = mapSize.y * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                float edge = side > 0f ? terrain.BoundsMax.y : terrain.BoundsMin.y;
                float depth = halfMapY - math.abs(edge) + mountainExtension;
                if (depth > 0f)
                {
                    Sprite sprite = side > 0f ? northMountainsSprite : southMountainsSprite;
                    CreateStretched("Mountains", sprite, borderSortingOrder + 3, new Vector2(0f, edge + side * depth * 0.5f), new Vector2(mapSize.x, depth));
                }
            }
        }

        /// <summary>
        /// За реками слева и справа — лес до края видимого: от края поля наружу на ширину спрайта, опушкой к реке.
        /// Лежит под рекой: вода и берег его закрывают, а между рекой и лесом виден подлесок.
        /// </summary>
        private void DrawOuterForest(TerrainMap terrain)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                Sprite sprite = side > 0f ? eastOuterForestSprite : westOuterForestSprite;
                if (sprite == null)
                {
                    continue;
                }

                float edge = side > 0f ? terrain.BoundsMax.x : terrain.BoundsMin.x;
                Vector2 size = sprite.bounds.size;
                CreateStretched("Outer Forest", sprite, borderSortingOrder - 1, new Vector2(edge + side * size.x * 0.5f, 0f), size);
            }
        }

        /// <summary>Горы сверху и снизу уходят в туман: градиент от середины гор и сплошной туман дальше.</summary>
        private void DrawFog()
        {
            if (fogSprite == null)
            {
                return;
            }

            float halfY = mapSize.y * 0.5f;
            float width = mapSize.x + outerExtent * 2f;
            foreach (float side in new[] { -1f, 1f })
            {
                float start = halfY - fogInset;
                CreateStretched("Fog", fogSprite, borderSortingOrder + 10,
                    new Vector2(0f, side * (start + fogDepth * 0.5f)), new Vector2(width, fogDepth), side > 0f ? 0f : 180f, Color.white);
                float solid = outerExtent;
                Create("Fog Solid", squareSprite, fogColor, borderSortingOrder + 10,
                    new Vector2(0f, side * (start + fogDepth + solid * 0.5f)), new Vector2(width, solid), 0f);
            }
        }

        private void DrawRoads(TerrainMap terrain)
        {
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

        private void DrawForests(TerrainMap terrain)
        {
            int seed = 0;
            foreach (Forest forest in terrain.Forests)
            {
                float diameter = forest.Radius * 2f;
                Create("Forest Floor", discSprite, forestFloorColor, forestFloorSortingOrder, forest.Center, new Vector2(diameter, diameter), 0f);

                int trees = math.max(4, (int)(math.PI * forest.Radius * forest.Radius * treeDensity));
                for (int i = 0; i < trees; i++)
                {
                    float random = Hash01(++seed);
                    float distance = forest.Radius * 0.85f * math.sqrt((i + 0.5f) / trees);
                    float angle = i * GoldenAngle;
                    float2 position = forest.Center + new float2(math.cos(angle), math.sin(angle)) * distance;
                    float size = math.lerp(treeSize.x, treeSize.y, random);
                    // Нижние деревья рисуются поверх верхних — кроны перекрываются как в перспективе.
                    int order = treeSortingOrder + (int)((forest.Center.y + forest.Radius - position.y) * 2f);
                    Create("Tree", treeSprite, Color.white, order, position, new Vector2(size, size), random * 40f - 20f);
                }
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

        /// <summary>Детерминированное псевдослучайное число в [0, 1) — горы и деревья стоят одинаково при каждом запуске.</summary>
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
