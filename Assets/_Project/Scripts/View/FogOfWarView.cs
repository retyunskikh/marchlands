using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// Туман войны на экране: непроглядная облачная пелена над всей картой (и лесами за краем), кроме кругов обзора
    /// вокруг своих отрядов, зданий (и строек) и деревень — радиус тот же, что у симуляции. Пелена плавно рассеивается,
    /// когда подходят свои, и довольно быстро возвращается, когда место потеряно. С воздушным шаром тумана нет.
    /// Рисуется одной текстурой (клетка — <see cref="cellSize"/>), сглаженной фильтрацией.
    /// </summary>
    public sealed class FogOfWarView : MonoBehaviour
    {
        [Tooltip("Размер клетки тумана, u: меньше — ровнее край, но дороже.")]
        [SerializeField] private float cellSize = 0.5f;
        [Tooltip("Насколько туман закрывает край снаружи радиуса обзора (мягкая кромка), u.")]
        [SerializeField] private float edgeSoftness = 3f;
        [Tooltip("Насколько туман выходит за карту (леса за краем, запас прокрутки), u.")]
        [SerializeField] private float margin = 40f;
        [Tooltip("За сколько секунд туман рассеивается, когда подходят свои.")]
        [SerializeField] private float revealTime = 0.8f;
        [Tooltip("За сколько секунд место уходит в туман, когда его потеряли.")]
        [SerializeField] private float coverTime = 0.35f;
        [SerializeField] private Color fogColor = new Color(0.76f, 0.78f, 0.8f, 1f);
        [SerializeField] private Color fogShadeColor = new Color(0.62f, 0.65f, 0.69f, 1f);
        [Tooltip("Размер «облаков» на пелене, u.")]
        [SerializeField] private float cloudScale = 9f;
        [SerializeField] private int sortingOrder = 1000;

        private Match match;
        private Side viewer;
        private Texture2D texture;
        private Color32[] pixels;
        private Color32[] cloud;
        private float[] shown;
        private byte[] target;
        private int width;
        private int height;
        private Vector2 origin;
        private int lastTick = -1;
        private bool settled;

        /// <param name="center">Центр карты.</param>
        /// <param name="size">Размер карты (туман выходит за неё на margin).</param>
        public void Initialize(Match match, Side viewer, Vector2 center, Vector2 size)
        {
            this.match = match;
            this.viewer = viewer;
            Vector2 covered = size + 2f * margin * Vector2.one;
            width = Mathf.Max(2, Mathf.CeilToInt(covered.x / cellSize));
            height = Mathf.Max(2, Mathf.CeilToInt(covered.y / cellSize));
            origin = center - covered * 0.5f;

            texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Fog of War"
            };
            pixels = new Color32[width * height];
            cloud = new Color32[width * height];
            shown = new float[width * height];
            target = new byte[width * height];
            BuildClouds();

            var spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), Vector2.zero, 1f / cellSize);
            spriteRenderer.sortingOrder = sortingOrder;
            transform.position = new Vector3(origin.x, origin.y, 0f);

            // С самого начала — сразу как есть, без рассеивания на старте.
            ComputeTarget();
            for (int i = 0; i < shown.Length; i++)
            {
                shown[i] = target[i] / 255f;
            }

            Upload();
        }

        private void LateUpdate()
        {
            if (match == null)
            {
                return;
            }

            if (match.CurrentTick != lastTick)
            {
                ComputeTarget();
                settled = false;
            }

            if (settled)
            {
                return;
            }

            float reveal = revealTime > 0f ? Time.deltaTime / revealTime : 1f;
            float cover = coverTime > 0f ? Time.deltaTime / coverTime : 1f;
            bool changed = false;
            bool moving = false;
            for (int i = 0; i < shown.Length; i++)
            {
                float goal = target[i] / 255f;
                float value = shown[i];
                if (value == goal)
                {
                    continue;
                }

                value = goal < value ? Mathf.Max(goal, value - reveal) : Mathf.Min(goal, value + cover);
                shown[i] = value;
                changed = true;
                moving |= value != goal;
            }

            if (changed)
            {
                Upload();
            }

            settled = !moving;
        }

        /// <summary>Куда стремится туман: 255 — закрыто, 0 — видно; круги обзора своих с мягкой кромкой снаружи.</summary>
        private void ComputeTarget()
        {
            lastTick = match.CurrentTick;
            bool clear = match.GetPlayer(viewer).HasBalloon;
            byte fill = clear ? (byte)0 : (byte)255;
            for (int i = 0; i < target.Length; i++)
            {
                target[i] = fill;
            }

            if (clear)
            {
                return;
            }

            float range = match.VisionRange;
            foreach (Squad squad in match.Squads)
            {
                if (squad.Side == viewer && squad.Alive > 0)
                {
                    Stamp(squad.Position, range);
                }
            }

            foreach (Building building in match.Buildings)
            {
                if (building.Owner == viewer && !building.IsDestroyed)
                {
                    Stamp(building.Position, range);
                }
            }

            foreach (Village village in match.Villages)
            {
                if (village.Owner == viewer)
                {
                    Stamp(village.Position, range);
                }
            }
        }

        private void Stamp(float2 center, float range)
        {
            float outer = range + edgeSoftness;
            int minX = Mathf.Max(0, Mathf.FloorToInt((center.x - outer - origin.x) / cellSize));
            int maxX = Mathf.Min(width - 1, Mathf.CeilToInt((center.x + outer - origin.x) / cellSize));
            int minY = Mathf.Max(0, Mathf.FloorToInt((center.y - outer - origin.y) / cellSize));
            int maxY = Mathf.Min(height - 1, Mathf.CeilToInt((center.y + outer - origin.y) / cellSize));
            float softness = Mathf.Max(edgeSoftness, 1e-3f);
            for (int y = minY; y <= maxY; y++)
            {
                float dy = origin.y + (y + 0.5f) * cellSize - center.y;
                int row = y * width;
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = origin.x + (x + 0.5f) * cellSize - center.x;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    if (distance >= outer)
                    {
                        continue;
                    }

                    // Внутри радиуса — чисто, снаружи туман плавно густеет на ширине edgeSoftness.
                    float fog = distance <= range ? 0f : Mathf.SmoothStep(0f, 1f, (distance - range) / softness);
                    var value = (byte)Mathf.RoundToInt(fog * 255f);
                    if (value < target[row + x])
                    {
                        target[row + x] = value;
                    }
                }
            }
        }

        private void Upload()
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 color = cloud[i];
                color.a = (byte)Mathf.RoundToInt(shown[i] * 255f);
                pixels[i] = color;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        /// <summary>Облачный узор пелены: два слоя шума Перлина, светлые и тёмные пятна.</summary>
        private void BuildClouds()
        {
            var random = new System.Random(12345);
            float offsetX = (float)random.NextDouble() * 1000f;
            float offsetY = (float)random.NextDouble() * 1000f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float wx = x * cellSize / cloudScale;
                    float wy = y * cellSize / cloudScale;
                    float noise = 0.65f * Mathf.PerlinNoise(wx + offsetX, wy + offsetY)
                        + 0.35f * Mathf.PerlinNoise(wx * 2.7f + offsetY, wy * 2.7f + offsetX);
                    cloud[y * width + x] = Color.Lerp(fogShadeColor, fogColor, Mathf.SmoothStep(0.2f, 0.8f, noise));
                }
            }
        }

        private void OnDestroy()
        {
            if (texture != null)
            {
                Destroy(texture);
            }
        }
    }
}
