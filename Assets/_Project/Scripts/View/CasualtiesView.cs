using System.Collections.Generic;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// Павшие солдаты: фигурка вспыхивает ранением, падает (поворачивается набок и темнеет), лежит
    /// и плавно исчезает. Фигурку отдаёт отряд — она живёт здесь и после гибели всего отряда.
    /// </summary>
    public sealed class CasualtiesView : MonoBehaviour
    {
        [SerializeField] private Color woundColor = new Color(1f, 0.35f, 0.3f);
        [Tooltip("Сколько длится вспышка ранения, s.")]
        [SerializeField] private float woundTime = 0.25f;
        [Tooltip("За сколько солдат падает, s.")]
        [SerializeField] private float fallTime = 0.35f;
        [Tooltip("Сколько лежит, s.")]
        [SerializeField] private float lieTime = 4f;
        [Tooltip("За сколько исчезает, s.")]
        [SerializeField] private float fadeTime = 1.5f;
        [SerializeField] private float lyingDarkening = 0.6f;
        [SerializeField] private int lyingSortingOrder = 15;

        private readonly List<Casualty> casualties = new List<Casualty>();

        private sealed class Casualty
        {
            public SpriteRenderer Renderer;
            public float Age;
            public float StartAngle;
            public float FallAngle;
            public Color BaseColor;
        }

        /// <summary>Забирает фигурку павшего солдата.</summary>
        public void Add(SpriteRenderer soldier)
        {
            soldier.transform.SetParent(transform, true);
            float start = soldier.transform.eulerAngles.z;
            // Падает набок — влево или вправо, по-разному для разных солдат.
            float side = (soldier.GetInstanceID() & 1) == 0 ? 1f : -1f;
            casualties.Add(new Casualty
            {
                Renderer = soldier,
                StartAngle = start,
                FallAngle = start + side * (70f + (soldier.GetInstanceID() & 15)),
                BaseColor = soldier.color
            });
        }

        private void Update()
        {
            float total = woundTime + fallTime + lieTime + fadeTime;
            for (int i = casualties.Count - 1; i >= 0; i--)
            {
                Casualty casualty = casualties[i];
                casualty.Age += Time.deltaTime;
                if (casualty.Age >= total)
                {
                    Destroy(casualty.Renderer.gameObject);
                    casualties.RemoveAt(i);
                    continue;
                }

                SpriteRenderer spriteRenderer = casualty.Renderer;
                Color lying = casualty.BaseColor * lyingDarkening;
                lying.a = casualty.BaseColor.a;
                if (casualty.Age < woundTime)
                {
                    float flash = Mathf.Sin(casualty.Age / woundTime * Mathf.PI);
                    spriteRenderer.color = Color.Lerp(casualty.BaseColor, woundColor, flash);
                    continue;
                }

                float fall = Mathf.Clamp01((casualty.Age - woundTime) / fallTime);
                float eased = 1f - (1f - fall) * (1f - fall);
                spriteRenderer.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(casualty.StartAngle, casualty.FallAngle, eased));
                Color color = Color.Lerp(casualty.BaseColor, lying, eased);
                if (fall >= 1f)
                {
                    spriteRenderer.sortingOrder = lyingSortingOrder;
                }

                float fadeStart = woundTime + fallTime + lieTime;
                color.a *= casualty.Age > fadeStart ? 1f - (casualty.Age - fadeStart) / fadeTime : 1f;
                spriteRenderer.color = color;
            }
        }
    }
}
