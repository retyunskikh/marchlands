using Marchlands.Simulation.Core;
using Marchlands.Simulation.Terrain;
using Unity.Mathematics;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// Местность полной карты: реки слева и справа, горы сверху и снизу, грунтовые дороги и рощи.
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

        [Header("Порядок отрисовки")]
        [SerializeField] private int roadSortingOrder = -80;
        [SerializeField] private int forestFloorSortingOrder = -75;
        [SerializeField] private int borderSortingOrder = -60;
        [SerializeField] private int treeSortingOrder = -45;

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
                float depth = halfMapY - math.abs(edge);
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
