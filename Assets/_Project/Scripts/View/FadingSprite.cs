using UnityEngine;

namespace Marchlands.View
{
    /// <summary>Брошенный на поле предмет (пушка разбитого расчёта): лежит, потом тает и исчезает.</summary>
    public sealed class FadingSprite : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private float lieTime;
        private float fadeTime;
        private float age;
        private float startAlpha;

        public void Initialize(SpriteRenderer target, float lie, float fade)
        {
            spriteRenderer = target;
            lieTime = lie;
            fadeTime = fade;
            startAlpha = target.color.a;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age < lieTime)
            {
                return;
            }

            float t = fadeTime > 0f ? (age - lieTime) / fadeTime : 1f;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            Color color = spriteRenderer.color;
            color.a = startAlpha * (1f - t);
            spriteRenderer.color = color;
        }
    }
}
