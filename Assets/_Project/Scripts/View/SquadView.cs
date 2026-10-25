using System.Collections.Generic;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// Фигурки солдат одного отряда, расставленные по слотам строя. Только читает состояние отряда
    /// и интерполирует его между тиками.
    /// </summary>
    public sealed class SquadView : MonoBehaviour
    {
        // Спрайт солдата нарисован фронтом вверх (+Y), а угол 0° в симуляции — это +X.
        private const float SpriteFacingOffset = -90f;

        private readonly List<SpriteRenderer> soldiers = new List<SpriteRenderer>();
        private readonly List<float2> fromSlots = new List<float2>();
        private readonly List<float2> toSlots = new List<float2>();

        private Squad squad;
        private SquadSettings settings;
        private SquadViewStyle style;
        private SpriteRenderer selectionMarker;

        public Squad Squad => squad;

        internal void Initialize(Squad squad, SquadSettings settings, SquadViewStyle style)
        {
            this.squad = squad;
            this.settings = settings;
            this.style = style;

            selectionMarker = CreateRenderer("Selection", style.MarkerSprite, style.SelectionColor, style.MarkerSortingOrder);
            selectionMarker.enabled = false;
        }

        internal void SetSelected(bool selected)
        {
            selectionMarker.enabled = selected;
        }

        internal void Render(float alpha)
        {
            float2 position = math.lerp(squad.PreviousPosition, squad.Position, alpha);
            float facing = squad.PreviousFacing + Angles.Delta(squad.PreviousFacing, squad.Facing) * alpha;

            SyncSoldierCount(squad.Alive);

            FormationLayout.GetLocalSlots(squad.Formation, squad.Alive, settings, fromSlots);
            float change = squad.IsChangingFormation ? squad.FormationChangeProgress : 0f;
            if (squad.IsChangingFormation)
            {
                FormationLayout.GetLocalSlots(squad.TargetFormation, squad.Alive, settings, toSlots);
            }

            Quaternion rotation = Quaternion.Euler(0f, 0f, facing + SpriteFacingOffset);
            for (int i = 0; i < soldiers.Count; i++)
            {
                float2 local = squad.IsChangingFormation ? math.lerp(fromSlots[i], toSlots[i], change) : fromSlots[i];
                float2 world = FormationLayout.LocalToWorld(local, position, facing);
                soldiers[i].transform.SetPositionAndRotation(new Vector3(world.x, world.y, 0f), rotation);
            }

            if (selectionMarker.enabled)
            {
                float2 half = FormationLayout.GetHalfExtents(squad.Formation, squad.Alive, settings);
                if (squad.IsChangingFormation)
                {
                    half = math.lerp(half, FormationLayout.GetHalfExtents(squad.TargetFormation, squad.Alive, settings), change);
                }

                Transform marker = selectionMarker.transform;
                marker.SetPositionAndRotation(new Vector3(position.x, position.y, 0f), rotation);
                marker.localScale = new Vector3((half.x + style.SelectionPadding) * 2f, (half.y + style.SelectionPadding) * 2f, 1f);
            }
        }

        private void SyncSoldierCount(int alive)
        {
            Color color = style.GetSideColor(squad.Side);
            while (soldiers.Count < alive)
            {
                soldiers.Add(CreateRenderer($"Soldier {soldiers.Count}", style.SoldierSprite, color, style.SoldierSortingOrder));
            }

            while (soldiers.Count > alive)
            {
                int last = soldiers.Count - 1;
                Destroy(soldiers[last].gameObject);
                soldiers.RemoveAt(last);
            }
        }

        private SpriteRenderer CreateRenderer(string objectName, Sprite sprite, Color color, int sortingOrder)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var spriteRenderer = child.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = sortingOrder;
            return spriteRenderer;
        }
    }
}
