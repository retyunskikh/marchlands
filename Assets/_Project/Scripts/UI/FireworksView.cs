using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Салют на экране победы: ракеты взлетают снизу, в верхней половине экрана рассыпаются разноцветными искрами,
    /// которые падают и гаснут. Только оформление; время — без учёта паузы.
    /// </summary>
    public sealed class FireworksView : MonoBehaviour
    {
        [SerializeField] private RectTransform area;
        [SerializeField] private Sprite sparkSprite;
        [Tooltip("Раз в столько секунд запускается ракета: от и до.")]
        [SerializeField] private Vector2 launchInterval = new Vector2(0.25f, 0.7f);
        [SerializeField] private int sparksPerBurst = 46;
        [Tooltip("Скорость разлёта искр, пикс./с: от и до.")]
        [SerializeField] private Vector2 burstSpeed = new Vector2(220f, 520f);
        [SerializeField] private float gravity = 260f;
        [SerializeField] private float sparkLifetime = 1.6f;
        [SerializeField] private float sparkSize = 14f;
        [SerializeField] private float rocketSpeed = 900f;
        [SerializeField] private Color[] palette =
        {
            new Color(1f, 0.85f, 0.3f), new Color(1f, 0.35f, 0.3f), new Color(0.45f, 0.7f, 1f),
            new Color(0.5f, 1f, 0.55f), new Color(1f, 0.55f, 0.95f), Color.white
        };

        private readonly List<Particle> particles = new List<Particle>();
        private readonly Stack<Image> pool = new Stack<Image>();
        private bool playing;
        private float nextLaunch;

        private sealed class Particle
        {
            public Image Image;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Age;
            public float Lifetime;
            public Color Color;
            public bool IsRocket;
            public float BurstHeight;
        }

        public void Play()
        {
            playing = true;
            nextLaunch = 0f;
            gameObject.SetActive(true);
        }

        public void Stop()
        {
            playing = false;
            foreach (Particle particle in particles)
            {
                Release(particle.Image);
            }

            particles.Clear();
        }

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            if (playing)
            {
                nextLaunch -= delta;
                if (nextLaunch <= 0f)
                {
                    Launch();
                    nextLaunch = Random.Range(launchInterval.x, launchInterval.y);
                }
            }

            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle particle = particles[i];
                particle.Age += delta;
                particle.Velocity.y -= gravity * delta * (particle.IsRocket ? 0.3f : 1f);
                particle.Position += particle.Velocity * delta;
                if (particle.IsRocket && particle.Position.y >= particle.BurstHeight)
                {
                    Burst(particle.Position, particle.Color);
                    Release(particle.Image);
                    particles.RemoveAt(i);
                    continue;
                }

                if (particle.Age >= particle.Lifetime)
                {
                    Release(particle.Image);
                    particles.RemoveAt(i);
                    continue;
                }

                float fade = particle.IsRocket ? 1f : 1f - particle.Age / particle.Lifetime;
                Color color = particle.Color;
                color.a = fade;
                particle.Image.color = color;
                particle.Image.rectTransform.anchoredPosition = particle.Position;
                float size = particle.IsRocket ? sparkSize * 0.8f : sparkSize * (0.6f + 0.6f * fade);
                particle.Image.rectTransform.sizeDelta = new Vector2(size, size);
            }
        }

        private void Launch()
        {
            Rect rect = area.rect;
            Color color = palette[Random.Range(0, palette.Length)];
            Image image = Take();
            particles.Add(new Particle
            {
                Image = image,
                Position = new Vector2(Random.Range(rect.xMin + rect.width * 0.1f, rect.xMax - rect.width * 0.1f), rect.yMin),
                Velocity = new Vector2(Random.Range(-60f, 60f), rocketSpeed),
                Lifetime = 5f,
                Color = color,
                IsRocket = true,
                BurstHeight = Random.Range(rect.yMin + rect.height * 0.5f, rect.yMax - rect.height * 0.1f)
            });
        }

        private void Burst(Vector2 center, Color color)
        {
            for (int i = 0; i < sparksPerBurst; i++)
            {
                float angle = (i + Random.value * 0.5f) / sparksPerBurst * Mathf.PI * 2f;
                float speed = Random.Range(burstSpeed.x, burstSpeed.y);
                // Часть искр — второго цвета, чтобы букет был пёстрым.
                Color sparkColor = Random.value < 0.25f ? palette[Random.Range(0, palette.Length)] : color;
                particles.Add(new Particle
                {
                    Image = Take(),
                    Position = center,
                    Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                    Lifetime = sparkLifetime * Random.Range(0.7f, 1.1f),
                    Color = sparkColor
                });
            }
        }

        private Image Take()
        {
            Image image = pool.Count > 0 ? pool.Pop() : CreateImage();
            image.gameObject.SetActive(true);
            return image;
        }

        private void Release(Image image)
        {
            image.gameObject.SetActive(false);
            pool.Push(image);
        }

        private Image CreateImage()
        {
            var child = new GameObject("Spark", typeof(RectTransform));
            child.transform.SetParent(area, false);
            var rectTransform = (RectTransform)child.transform;
            rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            var image = child.AddComponent<Image>();
            image.sprite = sparkSprite;
            image.raycastTarget = false;
            return image;
        }
    }
}
