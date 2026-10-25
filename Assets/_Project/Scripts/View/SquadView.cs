using System.Collections.Generic;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Morale;
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
        private SpriteRenderer moraleBackground;
        private SpriteRenderer moraleFill;

        private readonly List<SoldierRole> roles = new List<SoldierRole>();

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
            AssignRoles();

            selectionMarker = CreateRenderer("Selection", style.MarkerSprite, style.SelectionColor, style.MarkerSortingOrder);
            selectionMarker.enabled = false;
            moraleBackground = CreateRenderer("Morale", style.MarkerSprite, style.MoraleBarBackground, style.RingSortingOrder);
            moraleFill = CreateRenderer("Morale Fill", style.MarkerSprite, style.MoraleRoutingColor, style.RingSortingOrder + 1);
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
            float2 spacing = style.GetSpacing(squad.Type);
            bool openOrder = squad.Type == UnitType.Jaegers;
            for (int i = 0; i < soldiers.Count; i++)
            {
                float2 local = changing ? math.lerp(fromSlots[i], toSlots[transitionTargets[i]], change) : fromSlots[i];
                local *= spacing;
                if (squad.IsRouting)
                {
                    local = local * style.RoutSpread + Scatter(i) * style.RoutScatter;
                }
                else if (openOrder)
                {
                    local += Scatter(i + 101) * style.JaegerScatter;
                }

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

                half *= (float2)style.GetSpacing(squad.Type);

                Transform marker = selectionMarker.transform;
                marker.SetPositionAndRotation(new Vector3(position.x, position.y, 0f), rotation);
                marker.localScale = new Vector3((half.x + style.SelectionPadding) * 2f, (half.y + style.SelectionPadding) * 2f, 1f);
            }

            // Индикаторы стоят за тылом отряда: у сошедшихся в бою отрядов они не налезают на врага.
            float2 back = -Angles.Direction(facing);
            Vector2 barSize = style.MoraleBarSize;
            float barExtent = math.abs(back.x) * barSize.x * 0.5f + math.abs(back.y) * barSize.y * 0.5f;
            float2 barCenter = position + back * (GetRearDistance() + style.MoraleBarGap + barExtent);
            float2 ringCenter = barCenter + back * (barExtent + style.RingGap + style.RingRadius);

            RenderMoraleBar(barCenter);
            RenderRing(ringCenter, alpha);
        }

        /// <summary>Расстояние от центра отряда до его тыла.</summary>
        private float GetRearDistance()
        {
            float halfDepth = FormationLayout.GetHalfExtents(squad.Formation, squad.Alive, settings).y * style.GetSpacing(squad.Type).y;
            return squad.IsRouting ? halfDepth * style.RoutSpread + style.RoutScatter : halfDepth;
        }

        /// <summary>Горизонтальная полоска морали: заполнение слева направо, цвет по состоянию.</summary>
        private void RenderMoraleBar(float2 center)
        {
            Vector2 size = style.MoraleBarSize;
            moraleBackground.transform.SetPositionAndRotation(new Vector3(center.x, center.y, 0f), Quaternion.identity);
            moraleBackground.transform.localScale = new Vector3(size.x, size.y, 1f);

            float morale01 = math.saturate(squad.Morale / MoraleMath.MaxMorale);
            float width = size.x * morale01;
            moraleFill.enabled = width > 0f;
            moraleFill.color = squad.IsRouting ? style.MoraleRoutingColor : style.GetSteadyMoraleColor(morale01);
            moraleFill.transform.SetPositionAndRotation(
                new Vector3(center.x - size.x * 0.5f + width * 0.5f, center.y, 0f), Quaternion.identity);
            moraleFill.transform.localScale = new Vector3(width, size.y, 1f);
        }

        /// <summary>Постоянное для каждого солдата смещение в [-1, 1]² — толпа не дрожит от кадра к кадру.</summary>
        private static float2 Scatter(int index)
        {
            uint hash = (uint)index * 2654435761u;
            float x = ((hash >> 8) & 0xFFFF) / 32767.5f - 1f;
            float y = ((hash >> 16) * 2246822519u >> 16) / 32767.5f - 1f;
            return new float2(x, y);
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
        /// Кольцо за полоской морали: зелёное — заряжен, оранжевое — целится, заполняется белым — перезарядка.
        /// </summary>
        private void RenderRing(float2 ringCenter, float alpha)
        {
            var center = new Vector3(ringCenter.x, ringCenter.y, 0f);

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

        /// <summary>
        /// Роли по слотам полного отряда в линии: офицер — в центре первой шеренги, у гренадёров ещё знаменосец
        /// в центре второй и барабанщик на её левом краю. Слот 0 — левый край первой шеренги.
        /// </summary>
        private void AssignRoles()
        {
            FormationLayout.GetGrid(Formation.Line, squad.Alive, settings, out int files, out int ranks);
            roles.Clear();
            for (int i = 0; i < squad.Alive; i++)
            {
                roles.Add(SoldierRole.Private);
            }

            SetRole(files / 2, SoldierRole.Officer);
            if (squad.Type == UnitType.Grenadiers && ranks > 1)
            {
                SetRole(files + files / 2, SoldierRole.FlagBearer);
                SetRole(files, SoldierRole.Drummer);
            }
        }

        private void SetRole(int index, SoldierRole role)
        {
            if (index >= 0 && index < roles.Count)
            {
                roles[index] = role;
            }
        }

        private void SyncSoldierCount(int alive)
        {
            while (soldiers.Count < alive)
            {
                int index = soldiers.Count;
                SoldierRole role = index < roles.Count ? roles[index] : SoldierRole.Private;
                Sprite drawn = style.GetSoldierSprite(squad.Side, squad.Type, role);
                // Знаменосец поверх соседей: знамя накрывает их.
                int order = style.SoldierSortingOrder + (role == SoldierRole.FlagBearer ? 1 : 0);
                soldiers.Add(drawn != null
                    ? CreateRenderer($"Soldier {index}", drawn, Color.white, order)
                    : CreateRenderer($"Soldier {index}", style.SoldierSprite, style.GetSideColor(squad.Side), order));
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
