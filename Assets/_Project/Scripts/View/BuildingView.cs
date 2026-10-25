using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Config;
using Unity.Mathematics;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// Здание: тело цвета стороны (полупрозрачное во время постройки), полоска постройки или прихода
    /// следующего рекрута и рамка выделения. Только читает состояние здания.
    /// </summary>
    public sealed class BuildingView : MonoBehaviour
    {
        private Building building;
        private SimulationSettings settings;
        private BuildingViewStyle style;
        private Color sideColor;
        private Vector2 size;

        private SpriteRenderer body;
        private SpriteRenderer inner;
        private SpriteRenderer selection;
        private SpriteRenderer progressBackground;
        private SpriteRenderer progressFill;
        private SpriteRenderer flag;
        private Color innerColor;

        public Building Building => building;

        internal void Initialize(Building building, SimulationSettings settings, BuildingViewStyle style, Color sideColor)
        {
            this.building = building;
            this.settings = settings;
            this.style = style;
            this.sideColor = sideColor;
            transform.position = new Vector3(building.Position.x, building.Position.y, 0f);

            bool isHeadquarters = building.Type == BuildingType.Headquarters;
            size = isHeadquarters ? style.HeadquartersSize : style.BarracksSize;

            selection = CreateRenderer("Selection", style.BodySortingOrder - 1, style.SelectionColor);
            selection.transform.localScale = new Vector3(size.x + style.SelectionPadding * 2f, size.y + style.SelectionPadding * 2f, 1f);
            selection.enabled = false;

            if (isHeadquarters && style.HeadquartersBaseSprite != null && style.HeadquartersRoofSprite != null)
            {
                CreateHeadquarters();
            }
            else
            {
                body = CreateRenderer("Body", style.BodySortingOrder, sideColor);
                body.transform.localScale = new Vector3(size.x, size.y, 1f);

                // Без спрайтов штаб — квадрат с тёмной серединой; казарма — прямоугольник с тёмным коньком.
                inner = CreateRenderer("Inner", style.BodySortingOrder + 1, style.InnerColor);
                innerColor = style.InnerColor;
                inner.transform.localScale = isHeadquarters
                    ? new Vector3(size.x * style.HeadquartersInnerScale, size.y * style.HeadquartersInnerScale, 1f)
                    : new Vector3(size.x, size.y * 0.15f, 1f);
            }

            progressBackground = CreateRenderer("Progress", style.OverlaySortingOrder, style.ProgressBackground);
            progressBackground.transform.localPosition = new Vector3(0f, style.ProgressBarOffset, 0f);
            progressBackground.transform.localScale = new Vector3(style.ProgressBarSize.x, style.ProgressBarSize.y, 1f);
            progressFill = CreateRenderer("Progress Fill", style.OverlaySortingOrder + 1, style.ProgressColor);
        }

        /// <summary>
        /// Штаб-усадьба: ограда и двор (inner), крыши цвета стороны (body) и флаг во дворе.
        /// Ворота смотрят к центру карты: штаб справа отражается по горизонтали.
        /// </summary>
        private void CreateHeadquarters()
        {
            bool flip = building.Position.x > 0f;
            float direction = flip ? -1f : 1f;

            inner = CreateRenderer("Grounds", style.BodySortingOrder, Color.white);
            inner.sprite = style.HeadquartersBaseSprite;
            inner.flipX = flip;
            inner.transform.localScale = new Vector3(size.x, size.y, 1f);
            innerColor = Color.white;

            body = CreateRenderer("Roofs", style.BodySortingOrder + 1, sideColor);
            body.sprite = style.HeadquartersRoofSprite;
            body.flipX = flip;
            body.transform.localScale = new Vector3(size.x, size.y, 1f);

            Vector2 polePosition = Vector2.Scale(style.HeadquartersFlagPosition, size);
            polePosition.x *= direction;
            Vector2 flagSize = style.HeadquartersFlagSize;
            flag = CreateRenderer("Flag", style.BodySortingOrder + 2, sideColor);
            flag.transform.localPosition = polePosition + new Vector2(direction * flagSize.x * 0.5f, 0f);
            flag.transform.localScale = new Vector3(flagSize.x, flagSize.y, 1f);
            SpriteRenderer pole = CreateRenderer("Pole", style.BodySortingOrder + 3, style.PoleColor);
            pole.transform.localPosition = polePosition;
            pole.transform.localScale = new Vector3(style.HeadquartersPoleSize, style.HeadquartersPoleSize, 1f);
        }

        internal void SetSelected(bool selected)
        {
            selection.enabled = selected;
        }

        internal void Render()
        {
            Color bodyColor = sideColor;
            Color shownInnerColor = innerColor;
            if (!building.IsBuilt)
            {
                bodyColor.a *= style.ConstructionAlpha;
                shownInnerColor.a *= style.ConstructionAlpha;
            }

            body.color = bodyColor;
            inner.color = shownInnerColor;
            if (flag != null)
            {
                flag.color = bodyColor;
            }

            float progress = -1f;
            Color progressColor = style.ProgressColor;
            if (building.CaptureProgress > 0f)
            {
                // Враг у штаба — главная тревога, она важнее найма.
                progress = building.CaptureProgress / settings.victory.hqCaptureTime;
                progressColor = style.CaptureColor;
            }
            else if (!building.IsBuilt)
            {
                progress = building.BuildProgress;
            }
            else if (building.IsHiring && settings.buildings.hireInterval > 0f)
            {
                progress = 1f - building.HireRemaining / settings.buildings.hireInterval;
            }

            bool showProgress = progress >= 0f;
            progressBackground.enabled = showProgress;
            progressFill.enabled = showProgress;
            if (showProgress)
            {
                Vector2 bar = style.ProgressBarSize;
                float width = bar.x * math.saturate(progress);
                progressFill.color = progressColor;
                progressFill.transform.localPosition = new Vector3(-bar.x * 0.5f + width * 0.5f, style.ProgressBarOffset, 0f);
                progressFill.transform.localScale = new Vector3(width, bar.y, 1f);
            }
        }

        private SpriteRenderer CreateRenderer(string objectName, int sortingOrder, Color color)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var spriteRenderer = child.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = style.SquareSprite;
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.color = color;
            return spriteRenderer;
        }
    }
}
