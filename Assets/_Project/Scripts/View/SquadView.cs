using System.Collections.Generic;
using Marchlands.Simulation.Combat;
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
        private readonly List<int> transitionTargets = new List<int>();
        private readonly List<SpriteRenderer> ringSegments = new List<SpriteRenderer>();

        private Squad squad;
        private SquadSettings settings;
        private CombatSettings combat;
        private SquadViewStyle style;
        private SpriteRenderer selectionMarker;

        private Formation slotsFormation;
        private Formation slotsTarget;
        private int slotsAlive = -1;
        private bool slotsChanging;

        public Squad Squad => squad;

        internal void Initialize(Squad squad, SimulationSettings settings, SquadViewStyle style)
        {
            this.squad = squad;
            this.settings = settings.squads;
            combat = settings.combat;
            this.style = style;

            selectionMarker = CreateRenderer("Selection", style.MarkerSprite, style.SelectionColor, style.MarkerSortingOrder);
            selectionMarker.enabled = false;
            CreateRing();
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
            UpdateSlots();

            bool changing = squad.IsChangingFormation;
            float change = changing ? GetSmoothedChangeProgress(alpha) : 0f;

            Quaternion rotation = Quaternion.Euler(0f, 0f, facing + SpriteFacingOffset);
            for (int i = 0; i < soldiers.Count; i++)
            {
                float2 local = changing ? math.lerp(fromSlots[i], toSlots[transitionTargets[i]], change) : fromSlots[i];
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

            RenderRing(position, facing, alpha);
        }

        private void CreateRing()
        {
            float step = 360f / style.RingSegments;
            for (int i = 0; i < style.RingSegments; i++)
            {
                SpriteRenderer segment = CreateRenderer($"Ring {i}", style.MarkerSprite, style.RingEmptyColor, style.RingSortingOrder);
                // Сегменты идут по часовой стрелке от верхней точки кольца.
                float angle = 90f - (i + 0.5f) * step;
                segment.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                segment.transform.localScale = new Vector3(style.RingSegmentSize.x, style.RingSegmentSize.y, 1f);
                ringSegments.Add(segment);
            }
        }

        /// <summary>
        /// Кольцо над отрядом: зелёное — заряжен, оранжевое — целится, заполняется белым — перезарядка.
        /// Стоит над отрядом по оси Y мира независимо от его поворота.
        /// </summary>
        private void RenderRing(float2 position, float facing, float alpha)
        {
            float2 half = FormationLayout.GetHalfExtents(squad.Formation, squad.Alive, settings);
            float2 forward = Angles.Direction(facing);
            float2 right = Angles.Right(facing);
            float topOffset = math.abs(half.x * right.y) + math.abs(half.y * forward.y);
            var center = new Vector3(position.x, position.y + topOffset + style.RingGap + style.RingRadius, 0f);

            float filled;
            Color fillColor;
            switch (squad.FireState)
            {
                case FireState.Reloading:
                    float remaining = math.max(0f, squad.FireTimer - alpha * SimulationTime.TickDuration);
                    filled = 1f - remaining / combat.reloadTime;
                    fillColor = style.RingReloadingColor;
                    break;
                case FireState.Aiming:
                    filled = 1f;
                    fillColor = style.RingAimingColor;
                    break;
                default:
                    filled = 1f;
                    fillColor = style.RingLoadedColor;
                    break;
            }

            int filledCount = (int)math.floor(filled * ringSegments.Count);
            float step = 360f / ringSegments.Count;
            for (int i = 0; i < ringSegments.Count; i++)
            {
                float angle = math.radians(90f - (i + 0.5f) * step);
                Transform segment = ringSegments[i].transform;
                segment.position = center + new Vector3(math.cos(angle), math.sin(angle), 0f) * style.RingRadius;
                ringSegments[i].color = i < filledCount ? fillColor : style.RingEmptyColor;
            }
        }

        /// <summary>
        /// Пересчитывает слоты только при смене строя, численности или начале перестроения:
        /// сопоставление мест для перестроения должно оставаться одним и тем же до его конца.
        /// </summary>
        private void UpdateSlots()
        {
            bool changing = squad.IsChangingFormation;
            if (slotsFormation == squad.Formation && slotsTarget == squad.TargetFormation
                && slotsAlive == squad.Alive && slotsChanging == changing && fromSlots.Count == squad.Alive)
            {
                return;
            }

            slotsFormation = squad.Formation;
            slotsTarget = squad.TargetFormation;
            slotsAlive = squad.Alive;
            slotsChanging = changing;

            FormationLayout.GetLocalSlots(squad.Formation, squad.Alive, settings, fromSlots);
            if (changing)
            {
                FormationLayout.GetLocalSlots(squad.TargetFormation, squad.Alive, settings, toSlots);
                FormationLayout.MatchTransitionSlots(fromSlots, toSlots, transitionTargets);
            }
        }

        /// <summary>
        /// Прогресс перестроения симуляция обновляет раз в тик; между тиками досчитываем его по alpha
        /// и сглаживаем разгон и торможение.
        /// </summary>
        private float GetSmoothedChangeProgress(float alpha)
        {
            float perTick = SimulationTime.TickDuration / settings.formationChangeTime;
            float progress = math.saturate(squad.FormationChangeProgress + alpha * perTick);
            return math.smoothstep(0f, 1f, progress);
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
