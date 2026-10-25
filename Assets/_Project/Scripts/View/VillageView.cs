using System.Collections.Generic;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Unity.Mathematics;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// Деревня: домики, зона захвата с заливкой цвета владельца, флаг на древке и кольцо захвата по границе зоны.
    /// Кольцо заполняется по часовой стрелке цветом захватчика, мигает жёлтым, когда деревня оспаривается,
    /// и вспыхивает при смене владельца. Только читает состояние деревни.
    /// </summary>
    public sealed class VillageView : MonoBehaviour
    {
        private readonly List<SpriteRenderer> ringSegments = new List<SpriteRenderer>();

        private Village village;
        private float captureTime;
        private VillageViewStyle style;
        private SquadViewStyle sideStyle;

        private SpriteRenderer zoneFill;
        private SpriteRenderer zoneOutline;
        private SpriteRenderer flag;

        private Side? shownOwner;
        private float flashTimer;

        internal void Initialize(Village village, float captureRadius, float captureTime, VillageViewStyle style, SquadViewStyle sideStyle)
        {
            this.village = village;
            this.captureTime = captureTime;
            this.style = style;
            this.sideStyle = sideStyle;
            shownOwner = village.Owner;
            transform.position = new Vector3(village.Position.x, village.Position.y, 0f);

            float diameter = captureRadius * 2f;
            zoneFill = CreateRenderer("Zone", style.DiscSprite, style.GroundSortingOrder);
            zoneFill.transform.localScale = Vector3.one * diameter;
            zoneOutline = CreateRenderer("Zone Outline", style.CircleSprite, style.GroundSortingOrder + 1);
            zoneOutline.transform.localScale = Vector3.one * diameter;

            for (int i = 0; i < style.HouseOffsets.Length; i++)
            {
                SpriteRenderer house = CreateRenderer($"House {i}", style.HouseSprite, style.HouseSortingOrder);
                house.color = style.HouseColor;
                house.transform.localPosition = style.HouseOffsets[i];
                house.transform.localScale = Vector3.one * style.HouseSize;
            }

            SpriteRenderer pole = CreateRenderer("Pole", style.SquareSprite, style.HouseSortingOrder + 1);
            pole.color = style.PoleColor;
            Vector2 poleBase = style.FlagOffset - new Vector2(style.FlagSize.x * 0.5f, style.PoleSize.y * 0.5f - style.FlagSize.y * 0.5f);
            pole.transform.localPosition = poleBase;
            pole.transform.localScale = new Vector3(style.PoleSize.x, style.PoleSize.y, 1f);

            flag = CreateRenderer("Flag", style.SquareSprite, style.HouseSortingOrder + 2);
            flag.transform.localPosition = style.FlagOffset;
            flag.transform.localScale = new Vector3(style.FlagSize.x, style.FlagSize.y, 1f);

            CreateRing(captureRadius);
        }

        internal void Render()
        {
            if (village.Owner != shownOwner)
            {
                shownOwner = village.Owner;
                flashTimer = style.CaptureFlashDuration;
            }

            flashTimer = math.max(0f, flashTimer - Time.deltaTime);

            Color ownerColor = village.Owner.HasValue ? sideStyle.GetSideColor(village.Owner.Value) : style.NeutralColor;
            flag.color = ownerColor;
            zoneFill.color = WithAlpha(ownerColor, style.ZoneFillAlpha);
            zoneOutline.color = WithAlpha(ownerColor, style.ZoneOutlineAlpha);

            RenderRing(ownerColor);
        }

        private void CreateRing(float radius)
        {
            float step = 360f / style.RingSegments;
            float arcLength = 2f * math.PI * radius / style.RingSegments * style.RingSegmentFill;
            for (int i = 0; i < style.RingSegments; i++)
            {
                // Сегменты идут по часовой стрелке от верхней точки.
                float angle = 90f - (i + 0.5f) * step;
                float radians = math.radians(angle);
                SpriteRenderer segment = CreateRenderer($"Ring {i}", style.SquareSprite, style.OverlaySortingOrder);
                segment.transform.localPosition = new Vector3(math.cos(radians), math.sin(radians), 0f) * radius;
                segment.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                segment.transform.localScale = new Vector3(style.RingThickness, arcLength, 1f);
                ringSegments.Add(segment);
            }
        }

        /// <summary>
        /// Захват идёт — кольцо заполняется цветом захватчика; оспаривается — мигает жёлтым;
        /// только что захвачена — вспыхивает цветом нового владельца; иначе кольца не видно.
        /// </summary>
        private void RenderRing(Color ownerColor)
        {
            if (flashTimer > 0f)
            {
                Color flash = WithAlpha(ownerColor, flashTimer / style.CaptureFlashDuration);
                SetRing(ringSegments.Count, flash, flash);
                return;
            }

            bool capturing = village.CapturingSide.HasValue && village.CaptureProgress > 0f;
            if (!capturing && !village.IsContested)
            {
                SetRingVisible(false);
                return;
            }

            float progress = capturing ? math.saturate(village.CaptureProgress / captureTime) : 0f;
            int filled = (int)math.floor(progress * ringSegments.Count);
            Color fill = capturing ? sideStyle.GetSideColor(village.CapturingSide.Value) : style.ContestedColor;
            Color empty = style.RingEmptyColor;
            if (village.IsContested)
            {
                bool blinkOn = math.frac(Time.time * style.ContestedBlinkRate) < 0.5f;
                empty = blinkOn ? WithAlpha(style.ContestedColor, 0.8f) : style.RingEmptyColor;
            }

            SetRing(filled, fill, empty);
        }

        private void SetRing(int filled, Color fill, Color empty)
        {
            for (int i = 0; i < ringSegments.Count; i++)
            {
                ringSegments[i].enabled = true;
                ringSegments[i].color = i < filled ? fill : empty;
            }
        }

        private void SetRingVisible(bool visible)
        {
            foreach (SpriteRenderer segment in ringSegments)
            {
                segment.enabled = visible;
            }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
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
