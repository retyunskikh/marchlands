using System.Collections.Generic;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// Численность отряда числом «24/30» у полоски морали: при пополнении — зелёная, с подскоком и всплывающим «+1»,
    /// при потерях — на миг красная. Пока отряд пополняется в своей деревне, число светло-зелёное, а под ним
    /// полоска до следующего человека. Рисуется текстом в мире, с тёмной тенью для читаемости на траве.
    /// </summary>
    public sealed class SquadCountLabel : MonoBehaviour
    {
        private readonly List<Floater> floaters = new List<Floater>();

        private TextMesh text;
        private TextMesh shadow;
        private SpriteRenderer barBack;
        private SpriteRenderer barFill;
        private Font font;
        private SquadViewStyle style;
        private int shownAlive = -1;
        private float gainTimer;
        private float lossTimer;

        private sealed class Floater
        {
            public TextMesh Text;
            public float Age;
            public Vector3 Start;
        }

        internal void Initialize(SquadViewStyle style, int sortingOrder)
        {
            this.style = style;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            shadow = CreateText("Count Shadow", sortingOrder, transform);
            text = CreateText("Count", sortingOrder + 1, transform);
            var pixel = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0f, 0.5f), 4f);
            barBack = CreateBar("Reinforce Back", pixel, style.ReinforceBarBack, sortingOrder);
            barFill = CreateBar("Reinforce Fill", pixel, style.ReinforceBarFill, sortingOrder + 1);
        }

        private SpriteRenderer CreateBar(string objectName, Sprite sprite, Color color, int sortingOrder)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var bar = child.AddComponent<SpriteRenderer>();
            bar.sprite = sprite;
            bar.color = color;
            bar.sortingOrder = sortingOrder;
            return bar;
        }

        /// <summary>Показывает численность; рост и убыль сравниваются с прошлым показом.</summary>
        /// <param name="reinforceProgress">Доля до следующего человека из деревни; отрицательная — отряд не пополняется.</param>
        internal void Show(Vector3 position, int alive, int full, bool visible, float reinforceProgress)
        {
            text.gameObject.SetActive(visible);
            shadow.gameObject.SetActive(visible);
            bool reinforcing = visible && reinforceProgress >= 0f;
            barBack.gameObject.SetActive(reinforcing);
            barFill.gameObject.SetActive(reinforcing);
            if (shownAlive >= 0 && alive > shownAlive)
            {
                gainTimer = style.CountGainTime;
                SpawnFloater(position, "+" + (alive - shownAlive));
            }
            else if (shownAlive >= 0 && alive < shownAlive)
            {
                lossTimer = style.CountLossTime;
            }

            shownAlive = alive;
            gainTimer = Mathf.Max(0f, gainTimer - Time.deltaTime);
            lossTimer = Mathf.Max(0f, lossTimer - Time.deltaTime);
            UpdateFloaters();
            if (!visible)
            {
                return;
            }

            string value = alive + "/" + full;
            text.text = value;
            shadow.text = value;
            Color baseColor = reinforcing ? style.CountReinforceColor : style.CountColor;
            Color color = baseColor;
            float bounce = 0f;
            if (gainTimer > 0f)
            {
                float t = gainTimer / style.CountGainTime;
                color = Color.Lerp(baseColor, style.CountGainColor, Mathf.Clamp01(t * 2f));
                bounce = Mathf.Sin(t * Mathf.PI) * style.CountBounce;
            }
            else if (lossTimer > 0f)
            {
                color = Color.Lerp(baseColor, style.CountLossColor, lossTimer / style.CountLossTime);
            }

            if (reinforcing)
            {
                Vector2 size = style.ReinforceBarSize;
                var left = position + new Vector3(-size.x * 0.5f, -style.CountSize * 0.62f, 0f);
                barBack.transform.position = left;
                barBack.transform.localScale = new Vector3(size.x, size.y, 1f);
                barFill.transform.position = left;
                barFill.transform.localScale = new Vector3(size.x * Mathf.Clamp01(reinforceProgress), size.y, 1f);
            }

            text.color = color;
            text.transform.position = position + new Vector3(0f, bounce, 0f);
            shadow.transform.position = text.transform.position + new Vector3(0.04f, -0.04f, 0f);
        }

        private void SpawnFloater(Vector3 position, string value)
        {
            TextMesh floater = CreateText("Gain", text.GetComponent<MeshRenderer>().sortingOrder + 1, null);
            floater.text = value;
            floater.color = style.CountGainColor;
            floater.transform.position = position;
            floaters.Add(new Floater { Text = floater, Start = position });
        }

        private void UpdateFloaters()
        {
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                Floater floater = floaters[i];
                floater.Age += Time.deltaTime;
                float t = floater.Age / style.FloaterLifetime;
                if (t >= 1f)
                {
                    Destroy(floater.Text.gameObject);
                    floaters.RemoveAt(i);
                    continue;
                }

                floater.Text.transform.position = floater.Start + new Vector3(0f, t * style.FloaterRise, 0f);
                Color color = style.CountGainColor;
                color.a = 1f - t * t;
                floater.Text.color = color;
            }
        }

        private void OnDestroy()
        {
            foreach (Floater floater in floaters)
            {
                if (floater.Text != null)
                {
                    Destroy(floater.Text.gameObject);
                }
            }
        }

        private TextMesh CreateText(string objectName, int sortingOrder, Transform parent)
        {
            var child = new GameObject(objectName);
            if (parent != null)
            {
                child.transform.SetParent(parent, false);
            }

            var mesh = child.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.fontSize = 64;
            mesh.characterSize = style.CountSize / 64f * 10f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontStyle = FontStyle.Bold;
            mesh.color = new Color(0f, 0f, 0f, 0.75f);
            var meshRenderer = child.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = font.material;
            meshRenderer.sortingOrder = sortingOrder;
            return mesh;
        }
    }
}
