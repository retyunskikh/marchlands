using System.Collections.Generic;
using Marchlands.Simulation.Core;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Marchlands.View
{
    /// <summary>
    /// Дождь: косые струи по всему видимому полю, круги-всплески на земле, лёгкое затемнение и шум дождя.
    /// Плавно появляется и стихает, пока в симуляции идёт дождь (<see cref="Match.IsRaining"/>). Только вид:
    /// случайность здесь визуальная, через UnityEngine.Random. Звук — заглушка, шум генерируется при старте.
    /// </summary>
    public sealed class RainView : MonoBehaviour
    {
        [SerializeField] private Sprite streakSprite;
        [SerializeField] private Sprite splashSprite;
        [Tooltip("За сколько секунд дождь усиливается до полного и стихает.")]
        [SerializeField] private float fadeTime = 4f;

        [Header("Струи")]
        [SerializeField] private int streakCount = 220;
        [Tooltip("Длина и толщина струи в долях высоты экрана.")]
        [SerializeField] private Vector2 streakSize = new Vector2(0.05f, 0.0022f);
        [Tooltip("Скорость падения: экранов в секунду; снос ветром — доля от неё.")]
        [SerializeField] private Vector2 streakSpeed = new Vector2(1.3f, 1.9f);
        [SerializeField] private float wind = 0.28f;
        [SerializeField] private Color streakColor = new Color(0.82f, 0.88f, 0.95f, 0.38f);

        [Header("Всплески")]
        [SerializeField] private int splashCount = 70;
        [Tooltip("Размер круга в долях высоты экрана и сколько он живёт, s.")]
        [SerializeField] private float splashSize = 0.022f;
        [SerializeField] private float splashLife = 0.45f;
        [SerializeField] private Color splashColor = new Color(0.85f, 0.9f, 1f, 0.45f);

        [Header("Небо и звук")]
        [Tooltip("Затемнение поля в полный дождь.")]
        [SerializeField] private Color overlayColor = new Color(0.16f, 0.2f, 0.28f, 0.22f);
        [SerializeField] private float volume = 0.35f;
        [SerializeField] private int sortingOrder = 300;

        private readonly List<SpriteRenderer> streaks = new List<SpriteRenderer>();
        private readonly List<Vector3> streakState = new List<Vector3>();
        private readonly List<SpriteRenderer> splashes = new List<SpriteRenderer>();
        private readonly List<Vector3> splashState = new List<Vector3>();

        private Match match;
        private SpriteRenderer overlay;
        private AudioSource sound;
        private float intensity;

        public void Initialize(Match match)
        {
            this.match = match;
            overlay = CreateRenderer("Rain Overlay", streakSprite, sortingOrder - 1);
            for (int i = 0; i < streakCount; i++)
            {
                streaks.Add(CreateRenderer($"Rain {i}", streakSprite, sortingOrder));
                streakState.Add(new Vector3(Random.value, Random.value, Random.Range(streakSpeed.x, streakSpeed.y)));
            }

            for (int i = 0; i < splashCount; i++)
            {
                splashes.Add(CreateRenderer($"Splash {i}", splashSprite, sortingOrder - 2));
                splashState.Add(new Vector3(Random.value, Random.value, Random.value * splashLife));
            }

            sound = gameObject.AddComponent<AudioSource>();
            sound.clip = CreateRainClip();
            sound.loop = true;
            sound.playOnAwake = false;
            sound.spatialBlend = 0f;
            sound.volume = 0f;
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (match == null)
            {
                return;
            }

            float step = fadeTime > 0f ? Time.deltaTime / fadeTime : 1f;
            intensity = match.IsRaining ? math.min(1f, intensity + step) : math.max(0f, intensity - step);
            bool visible = intensity > 0f;
            SetVisible(visible);
            sound.volume = volume * intensity;
            if (visible && !sound.isPlaying)
            {
                sound.Play();
            }
            else if (!visible && sound.isPlaying)
            {
                sound.Stop();
            }

            Camera view = Camera.main;
            if (!visible || view == null)
            {
                return;
            }

            float height = view.orthographicSize * 2f;
            float width = height * view.aspect;
            Vector3 origin = view.transform.position - new Vector3(width * 0.5f, height * 0.5f, 0f);
            origin.z = 0f;

            overlay.transform.position = origin + new Vector3(width * 0.5f, height * 0.5f, 0f);
            overlay.transform.localScale = new Vector3(width / streakSprite.bounds.size.x, height / streakSprite.bounds.size.y, 1f);
            Color dim = overlayColor;
            dim.a *= intensity;
            overlay.color = dim;

            RenderStreaks(origin, width, height, view.aspect);
            RenderSplashes(origin, width, height);
        }

        /// <summary>Струи живут в долях экрана: падают наискосок и, уйдя за низ, появляются сверху в новом месте.</summary>
        private void RenderStreaks(Vector3 origin, float width, float height, float aspect)
        {
            float angle = math.degrees(math.atan2(-1f, wind)) + 90f;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
            Vector2 spriteSize = streakSprite.bounds.size;
            var scale = new Vector3(streakSize.y * height / spriteSize.x, streakSize.x * height / spriteSize.y, 1f);
            int shown = (int)(streaks.Count * math.saturate(intensity * 1.3f));
            for (int i = 0; i < streaks.Count; i++)
            {
                SpriteRenderer streak = streaks[i];
                streak.enabled = i < shown;
                Vector3 state = streakState[i];
                state.y -= state.z * Time.deltaTime;
                state.x += state.z * wind / aspect * Time.deltaTime;
                if (state.y < -0.05f)
                {
                    state.y = 1.05f;
                    state.x = Random.Range(-0.2f, 1f);
                }

                if (state.x > 1.05f)
                {
                    state.x -= 1.1f;
                }

                streakState[i] = state;
                if (!streak.enabled)
                {
                    continue;
                }

                streak.transform.SetPositionAndRotation(origin + new Vector3(state.x * width, state.y * height, 0f), rotation);
                streak.transform.localScale = scale;
                Color color = streakColor;
                color.a *= 0.6f + 0.4f * ((i * 37) % 10) / 10f;
                streak.color = color;
            }
        }

        /// <summary>Круги на земле: расходятся и тают, на смену появляется новый в другом месте экрана.</summary>
        private void RenderSplashes(Vector3 origin, float width, float height)
        {
            Vector2 spriteSize = splashSprite.bounds.size;
            int shown = (int)(splashes.Count * intensity);
            for (int i = 0; i < splashes.Count; i++)
            {
                Vector3 state = splashState[i];
                state.z += Time.deltaTime;
                if (state.z >= splashLife)
                {
                    state = new Vector3(Random.value, Random.value, 0f);
                }

                splashState[i] = state;
                SpriteRenderer splash = splashes[i];
                splash.enabled = i < shown;
                if (!splash.enabled)
                {
                    continue;
                }

                float t = state.z / splashLife;
                float size = splashSize * height * math.lerp(0.3f, 1f, t);
                splash.transform.position = origin + new Vector3(state.x * width, state.y * height, 0f);
                splash.transform.localScale = new Vector3(size / spriteSize.x, size * 0.6f / spriteSize.y, 1f);
                Color color = splashColor;
                color.a *= 1f - t;
                splash.color = color;
            }
        }

        private void SetVisible(bool visible)
        {
            if (overlay.enabled == visible)
            {
                return;
            }

            overlay.enabled = visible;
            foreach (SpriteRenderer streak in streaks)
            {
                streak.enabled = visible;
            }

            foreach (SpriteRenderer splash in splashes)
            {
                splash.enabled = visible;
            }
        }

        /// <summary>
        /// Шум дождя: мягкий «розовый» шум и частые капли-щелчки разной силы; 4 секунды, зациклено без шва
        /// (конец плавно переходит в начало).
        /// </summary>
        private static AudioClip CreateRainClip()
        {
            const int rate = 22050;
            const float seconds = 4f;
            int length = (int)(rate * seconds);
            var data = new float[length];
            var random = new System.Random(4242);
            float b0 = 0f, b1 = 0f, b2 = 0f;
            for (int i = 0; i < length; i++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                // Розовый шум (фильтр Пола Келлета, упрощённый).
                b0 = 0.99765f * b0 + white * 0.0990460f;
                b1 = 0.96300f * b1 + white * 0.2965164f;
                b2 = 0.57000f * b2 + white * 1.0526913f;
                data[i] = (b0 + b1 + b2 + white * 0.1848f) * 0.11f;
            }

            // Капли: короткие затухающие щелчки.
            int drops = (int)(seconds * 140f);
            for (int d = 0; d < drops; d++)
            {
                int start = random.Next(length);
                float strength = (float)(0.05 + random.NextDouble() * 0.2);
                int dropLength = rate / 200;
                for (int k = 0; k < dropLength; k++)
                {
                    int index = (start + k) % length;
                    float decay = 1f - k / (float)dropLength;
                    data[index] += (float)(random.NextDouble() * 2.0 - 1.0) * strength * decay * decay;
                }
            }

            // Сшивка конца с началом: последние 0.2 с плавно переходят в первые.
            int blend = rate / 5;
            for (int i = 0; i < blend; i++)
            {
                float t = i / (float)blend;
                int tail = length - blend + i;
                data[tail] = math.lerp(data[tail], data[i], t);
            }

            for (int i = 0; i < length; i++)
            {
                data[i] = math.clamp(data[i], -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Rain", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private SpriteRenderer CreateRenderer(string objectName, Sprite sprite, int order)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var spriteRenderer = child.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = order;
            return spriteRenderer;
        }
    }
}
