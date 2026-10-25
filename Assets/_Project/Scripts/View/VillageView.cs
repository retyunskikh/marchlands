using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Unity.Mathematics;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// Деревня: домики, круг радиуса захвата и флажок цвета владельца, полоска прогресса цвета захватчика.
    /// Только читает состояние деревни.
    /// </summary>
    public sealed class VillageView : MonoBehaviour
    {
        private Village village;
        private float captureTime;
        private VillageViewStyle style;
        private SquadViewStyle sideStyle;

        private SpriteRenderer radius;
        private SpriteRenderer flag;
        private SpriteRenderer progressBackground;
        private SpriteRenderer progressFill;

        internal void Initialize(Village village, float captureRadius, float captureTime, VillageViewStyle style, SquadViewStyle sideStyle)
        {
            this.village = village;
            this.captureTime = captureTime;
            this.style = style;
            this.sideStyle = sideStyle;
            transform.position = new Vector3(village.Position.x, village.Position.y, 0f);

            radius = CreateRenderer("Capture Radius", style.CircleSprite, style.GroundSortingOrder);
            radius.transform.localScale = Vector3.one * (captureRadius * 2f);

            for (int i = 0; i < style.HouseOffsets.Length; i++)
            {
                SpriteRenderer house = CreateRenderer($"House {i}", style.HouseSprite, style.HouseSortingOrder);
                house.color = style.HouseColor;
                house.transform.localPosition = style.HouseOffsets[i];
                house.transform.localScale = Vector3.one * style.HouseSize;
            }

            flag = CreateRenderer("Flag", style.SquareSprite, style.HouseSortingOrder + 1);
            flag.transform.localPosition = style.FlagOffset;
            flag.transform.localScale = new Vector3(style.FlagSize.x, style.FlagSize.y, 1f);

            progressBackground = CreateRenderer("Capture", style.SquareSprite, style.OverlaySortingOrder);
            progressBackground.color = style.ProgressBackground;
            progressBackground.transform.localPosition = new Vector3(0f, style.ProgressBarOffset, 0f);
            progressBackground.transform.localScale = new Vector3(style.ProgressBarSize.x, style.ProgressBarSize.y, 1f);
            progressFill = CreateRenderer("Capture Fill", style.SquareSprite, style.OverlaySortingOrder + 1);
        }

        internal void Render()
        {
            Color ownerColor = village.Owner.HasValue ? sideStyle.GetSideColor(village.Owner.Value) : style.NeutralColor;
            flag.color = ownerColor;
            Color radiusColor = ownerColor;
            radiusColor.a = style.RadiusAlpha;
            radius.color = radiusColor;

            bool capturing = village.CapturingSide.HasValue && village.CaptureProgress > 0f;
            progressBackground.enabled = capturing;
            progressFill.enabled = capturing;
            if (!capturing)
            {
                return;
            }

            Vector2 size = style.ProgressBarSize;
            float width = size.x * math.saturate(village.CaptureProgress / captureTime);
            progressFill.color = sideStyle.GetSideColor(village.CapturingSide.Value);
            progressFill.transform.localPosition = new Vector3(-size.x * 0.5f + width * 0.5f, style.ProgressBarOffset, 0f);
            progressFill.transform.localScale = new Vector3(width, size.y, 1f);
        }

        private SpriteRenderer CreateRenderer(string objectName, Sprite sprite, int sortingOrder)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var spriteRenderer = child.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = sortingOrder;
            return spriteRenderer;
        }
    }
}
