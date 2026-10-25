using System.Collections.Generic;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Config;
using Unity.Mathematics;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// Здание: тело цвета стороны (полупрозрачное во время постройки), полоска постройки или найма,
    /// точки очереди найма и рамка выделения. Только читает состояние здания.
    /// </summary>
    public sealed class BuildingView : MonoBehaviour
    {
        private readonly List<SpriteRenderer> queueDots = new List<SpriteRenderer>();

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

            body = CreateRenderer("Body", style.BodySortingOrder, sideColor);
            body.transform.localScale = new Vector3(size.x, size.y, 1f);

            // Штаб — квадрат с тёмной серединой, казарма — прямоугольник с тёмным коньком.
            inner = CreateRenderer("Inner", style.BodySortingOrder + 1, style.InnerColor);
            inner.transform.localScale = isHeadquarters
                ? new Vector3(size.x * style.HeadquartersInnerScale, size.y * style.HeadquartersInnerScale, 1f)
                : new Vector3(size.x, size.y * 0.15f, 1f);

            progressBackground = CreateRenderer("Progress", style.OverlaySortingOrder, style.ProgressBackground);
            progressBackground.transform.localPosition = new Vector3(0f, style.ProgressBarOffset, 0f);
            progressBackground.transform.localScale = new Vector3(style.ProgressBarSize.x, style.ProgressBarSize.y, 1f);
            progressFill = CreateRenderer("Progress Fill", style.OverlaySortingOrder + 1, style.ProgressColor);
        }

        internal void SetSelected(bool selected)
        {
            selection.enabled = selected;
        }

        internal void Render()
        {
            Color bodyColor = sideColor;
            Color innerColor = style.InnerColor;
            if (!building.IsBuilt)
            {
                bodyColor.a *= style.ConstructionAlpha;
                innerColor.a *= style.ConstructionAlpha;
            }

            body.color = bodyColor;
            inner.color = innerColor;

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
            else if (building.RecruitQueue > 0)
            {
                progress = 1f - building.RecruitRemaining / settings.buildings.recruitTime;
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

            RenderQueue();
        }

        /// <summary>Точки под полоской — сколько отрядов в очереди найма.</summary>
        private void RenderQueue()
        {
            while (queueDots.Count < building.RecruitQueue)
            {
                SpriteRenderer dot = CreateRenderer($"Queue {queueDots.Count}", style.OverlaySortingOrder, style.ProgressColor);
                dot.transform.localScale = new Vector3(style.QueueDotSize.x, style.QueueDotSize.y, 1f);
                queueDots.Add(dot);
            }

            float y = style.ProgressBarOffset - style.ProgressBarSize.y - style.QueueDotSize.y;
            float start = -(building.RecruitQueue - 1) * style.QueueDotSpacing * 0.5f;
            for (int i = 0; i < queueDots.Count; i++)
            {
                bool used = i < building.RecruitQueue;
                queueDots[i].enabled = used;
                if (used)
                {
                    queueDots[i].transform.localPosition = new Vector3(start + i * style.QueueDotSpacing, y, 0f);
                }
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
