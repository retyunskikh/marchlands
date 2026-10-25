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
        [SerializeField] private Sprite mountainSprite;

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

        [Header("Реки")]
        [SerializeField] private Color riverColor = new Color(0.25f, 0.45f, 0.66f);
        [SerializeField] private Color riverHighlight = new Color(0.4f, 0.6f, 0.8f);
        [SerializeField] private Color bankColor = new Color(0.76f, 0.7f, 0.5f);
        [SerializeField] private float bankWidth = 0.8f;

        [Header("Горы")]
        [SerializeField] private Color mountainBaseColor = new Color(0.42f, 0.38f, 0.33f);
        [Tooltip("Высота вершин как доля глубины полосы гор: от и до.")]
        [SerializeField] private Vector2 peakHeightRange = new Vector2(0.7f, 1f);
        [Tooltip("Ширина вершины к её высоте.")]
        [SerializeField] private float peakAspect = 1.4f;
        [SerializeField] private float peakSpacing = 4.5f;

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

            DrawRivers(terrain);
            DrawMountains(terrain);
            DrawRoads(terrain);
            DrawForests(terrain);
        }

        private void DrawRivers(TerrainMap terrain)
        {
            float halfMapX = mapSize.x * 0.5f;
            float height = mapSize.y;
            foreach (float side in new[] { -1f, 1f })
            {
                float edge = side > 0f ? terrain.BoundsMax.x : terrain.BoundsMin.x;
                float width = halfMapX - math.abs(edge);
                if (width <= 0f)
                {
                    continue;
                }

                float center = edge + side * width * 0.5f;
                Create("River", squareSprite, riverColor, borderSortingOrder, new Vector2(center, 0f), new Vector2(width, height), 0f);
                Create("River Glint", squareSprite, riverHighlight, borderSortingOrder + 1, new Vector2(center, 0f), new Vector2(width * 0.25f, height), 0f);
                Create("Bank", squareSprite, bankColor, borderSortingOrder + 2, new Vector2(edge + side * bankWidth * 0.5f, 0f), new Vector2(bankWidth, height), 0f);
            }
        }

        /// <summary>
        /// Горы сверху и снизу: тёмная подложка и ряд стоящих вершин (у спрайта точка опоры внизу).
        /// Сверху вершины растут от границы поля наружу, снизу — от края карты до границы поля;
        /// высота не больше глубины полосы, чтобы вершины не залезали на поле.
        /// </summary>
        private void DrawMountains(TerrainMap terrain)
        {
            float halfMapY = mapSize.y * 0.5f;
            float width = mapSize.x;
            foreach (float side in new[] { -1f, 1f })
            {
                float edge = side > 0f ? terrain.BoundsMax.y : terrain.BoundsMin.y;
                float depth = halfMapY - math.abs(edge);
                if (depth <= 0f)
                {
                    continue;
                }

                Create("Mountains", squareSprite, mountainBaseColor, borderSortingOrder + 3, new Vector2(0f, edge + side * depth * 0.5f), new Vector2(width, depth), 0f);

                float baseY = side > 0f ? edge : -halfMapY;
                int peaks = (int)math.ceil(width / peakSpacing) + 1;
                for (int i = 0; i < peaks; i++)
                {
                    float random = Hash01(i * 2 + (side > 0f ? 1 : 0));
                    float x = -width * 0.5f + i * peakSpacing + (random - 0.5f) * peakSpacing * 0.6f;
                    float height = depth * math.lerp(peakHeightRange.x, peakHeightRange.y, random);
                    var size = new Vector2(height * peakAspect, height);
                    Create("Peak", mountainSprite, Color.white, borderSortingOrder + 4 + (int)(random * 3f), new Vector2(x, baseY), size, 0f);
                }
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
