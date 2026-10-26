using System.Collections.Generic;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Melee;
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
        private Match match;
        private SquadSettings settings;
        private CombatSettings combat;
        private SquadViewStyle style;
        private SpriteRenderer selectionMarker;
        private SpriteRenderer moraleBackground;
        private SpriteRenderer moraleFill;

        private readonly List<SoldierRole> roles = new List<SoldierRole>();

        // Для рукопашной: обычная фигурка, тело без оружия и руки с оружием (null — без выпада).
        private readonly List<Sprite> fullSprites = new List<Sprite>();
        private readonly List<Sprite> bodySprites = new List<Sprite>();
        private readonly List<SoldierRole> soldierRoles = new List<SoldierRole>();

        // Где солдат стоит в строю сейчас (локально): плавно догоняет своё место после потерь и перестроек.
        private readonly List<float2> shownSlots = new List<float2>();
        private CasualtiesView casualties;
        private SquadCountLabel countLabel;
        private readonly List<SpriteRenderer> arms = new List<SpriteRenderer>();

        private Formation slotsFormation;
        private Formation slotsTarget;
        private int slotsAlive = -1;
        private bool slotsChanging;

        // 0 — стоит, 1 — идёт; меняется плавно, чтобы походка не обрывалась.
        private float gaitBlend;

        // 0 — обычный строй, 1 — полная свалка рукопашной; меняется плавно.
        private float meleeBlend;
        private float aimBlend;
        private float2 meleeContact;
        private float2 meleeDirection = new float2(0f, 1f);

        public Squad Squad => squad;

        internal void Initialize(Squad squad, Match match, SquadViewStyle style, CasualtiesView casualties)
        {
            this.squad = squad;
            this.casualties = casualties;
            this.match = match;
            settings = match.Settings.squads;
            combat = match.Settings.combat;
            this.style = style;
            AssignRoles();

            selectionMarker = CreateRenderer("Selection", style.MarkerSprite, style.SelectionColor, style.MarkerSortingOrder);
            selectionMarker.enabled = false;
            moraleBackground = CreateRenderer("Morale", style.MarkerSprite, style.MoraleBarBackground, style.RingSortingOrder);
            moraleFill = CreateRenderer("Morale Fill", style.MarkerSprite, style.MoraleRoutingColor, style.RingSortingOrder + 1);
            CreateRing();
            countLabel = gameObject.AddComponent<SquadCountLabel>();
            countLabel.Initialize(style, style.RingSortingOrder + 2);
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
            UpdateMelee(alpha, position);
            UpdateGait();
            UpdateAim();
            int shooters = VolleyMath.Shooters(squad.Formation, squad.Alive, settings);
            float2 forward = Angles.Direction(facing);
            Quaternion meleeRotation = Quaternion.Slerp(rotation,
                Quaternion.Euler(0f, 0f, Angles.Of(meleeDirection) + SpriteFacingOffset), meleeBlend);
            float2 spacing = style.GetSpacing(squad.Type);
            float regroup = 1f - math.exp(-style.RegroupSpeed * Time.deltaTime);
            for (int i = 0; i < soldiers.Count; i++)
            {
                float2 local = changing ? math.lerp(fromSlots[i], toSlots[transitionTargets[i]], change) : fromSlots[i];
                local *= spacing;
                if (squad.IsRouting)
                {
                    local = local * style.RoutSpread + Scatter(i) * style.RoutScatter;
                }

                // Место в строю могло смениться (павшие, пополнение) — солдат идёт к нему, а не перескакивает.
                if (float.IsNaN(shownSlots[i].x))
                {
                    shownSlots[i] = local;
                }

                local = shownSlots[i] = math.lerp(shownSlots[i], local, regroup);

                if (gaitBlend > 0f)
                {
                    local += GaitOffset(i, local);
                }

                float2 world = FormationLayout.LocalToWorld(local, position, facing);
                if (meleeBlend > 0f)
                {
                    world = ToMeleePosition(world, i);
                }

                soldiers[i].transform.SetPositionAndRotation(new Vector3(world.x, world.y, 0f), meleeBlend > 0f ? meleeRotation : rotation);
                bool posed = RenderArms(i, world, rotation, meleeRotation, forward, i < shooters);
                if (!posed)
                {
                    RenderStep(i, local);
                }
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

            // Численность — сбоку от полоски морали, поперёк направления в тыл (там кольцо перезарядки), ближе к верху экрана.
            float2 across = new float2(-back.y, back.x);
            if (across.y < 0f || (across.y == 0f && across.x < 0f))
            {
                across = -across;
            }

            float barAcross = math.abs(across.x) * barSize.x * 0.5f + math.abs(across.y) * barSize.y * 0.5f;
            float2 countAt = barCenter + across * (barAcross + style.CountGap);
            countLabel.Show(
                new Vector3(countAt.x, countAt.y, 0f), squad.Alive, UnitStats.Of(squad, match.Settings).squadSize, true,
                squad.IsReinforcing ? squad.ReinforceProgress : -1f);
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

        /// <summary>Идёт ли отряд: сдвинулся за последний тик (бегущие и атакующие тоже идут).</summary>
        /// <summary>«Целься!»: стреляющие вскидывают ружья, пока отряд прицеливается, и опускают после залпа.</summary>
        private void UpdateAim()
        {
            bool aiming = squad.FireState == FireState.Aiming && !squad.IsRouting;
            float time = aiming ? style.AimRaiseTime : style.AimLowerTime;
            float step = time > 0f ? Time.deltaTime / time : 1f;
            aimBlend = aiming ? math.min(1f, aimBlend + step) : math.max(0f, aimBlend - step);
        }

        private void UpdateGait()
        {
            bool moving = math.distancesq(squad.PreviousPosition, squad.Position) > 1e-6f && !squad.IsInMelee;
            float step = style.GaitBlendTime > 0f ? Time.deltaTime / style.GaitBlendTime : 1f;
            gaitBlend = moving ? math.min(1f, gaitBlend + step) : math.max(0f, gaitBlend - step);
        }

        /// <summary>
        /// Смещение солдата в строю от походки (вид сверху: покачивание с ноги на ногу и рывки вперёд).
        /// Пехота марширует в ногу — у всех одна фаза; конница скачет —
        /// рывками вперёд-назад, по шеренгам с запаздыванием.
        /// </summary>
        private float2 GaitOffset(int index, float2 local)
        {
            Vector2 gait = style.GetGait(squad.Type);
            float time = Time.time * gait.x * 2f * math.PI;
            float amplitude = gait.y * math.smoothstep(0f, 1f, gaitBlend);
            switch (squad.Type)
            {
                case UnitType.Cavalry:
                {
                    // Шеренга сзади отстаёт по фазе — волна скачков.
                    float phase = -local.y * 1.3f + Scatter(index + 613).x * 0.4f;
                    return new float2(0f, math.sin(time + phase) * amplitude);
                }

                default:
                    return new float2(math.sin(time) * amplitude, math.abs(math.cos(time)) * amplitude * 0.5f);
            }
        }

        /// <summary>
        /// Линия схватки — середина между отрядом и ближайшим противником по рукопашной. Пока отряд дерётся,
        /// строй плавно сходится в свалку, а после — расходится обратно.
        /// </summary>
        private void UpdateMelee(float alpha, float2 position)
        {
            Squad opponent = null;
            float best = float.MaxValue;
            foreach (MeleeEngagement engagement in match.Engagements)
            {
                if (!engagement.Involves(squad))
                {
                    continue;
                }

                Squad other = engagement.Attacker == squad ? engagement.Defender : engagement.Attacker;
                float distance = math.distancesq(other.Position, squad.Position);
                if (distance < best)
                {
                    best = distance;
                    opponent = other;
                }
            }

            float step = style.MeleeBlendTime > 0f ? Time.deltaTime / style.MeleeBlendTime : 1f;
            if (opponent != null && !squad.IsRouting)
            {
                float2 opponentPosition = math.lerp(opponent.PreviousPosition, opponent.Position, alpha);
                float2 toOpponent = opponentPosition - position;
                if (math.lengthsq(toOpponent) > 1e-6f)
                {
                    meleeDirection = math.normalize(toOpponent);
                }

                meleeContact = (position + opponentPosition) * 0.5f;
                meleeBlend = math.min(1f, meleeBlend + step);
            }
            else
            {
                meleeBlend = math.max(0f, meleeBlend - step);
            }
        }

        /// <summary>
        /// Свалка: строй сжимается к линии схватки, солдаты сбиваются с мест и по очереди делают выпады к врагу.
        /// Сторону линии солдат не меняет — свои остаются со своей стороны.
        /// </summary>
        private float2 ToMeleePosition(float2 world, int index)
        {
            float2 across = new float2(-meleeDirection.y, meleeDirection.x);
            float2 offset = world - meleeContact;
            float depth = math.dot(offset, meleeDirection);
            float lateral = math.dot(offset, across);

            float blend = math.smoothstep(0f, 1f, meleeBlend);
            float compressed = math.min(depth * (1f - style.MeleeCompress * blend), -style.MeleeGap * blend);
            float2 scatter = Scatter(index + 503) * style.MeleeScatter * blend;
            // Тело лишь чуть подаётся вперёд за выпадом — колют руки (см. RenderArms).
            compressed += Thrust(index) * style.ThrustLength * 0.25f * blend;

            return meleeContact + meleeDirection * compressed + across * lateral * (1f - 0.2f * blend) + scatter;
        }

        /// <summary>
        /// Кадр шага: на ходу фигурка чередует «левой» и «правой» (у конницы — галоп), стоя — обычная.
        /// Пехота шагает в ногу, конница — волной по шеренгам (та же фаза, что у походки).
        /// </summary>
        private void RenderStep(int index, float2 local)
        {
            Sprite stand = fullSprites[index];
            if (stand == null)
            {
                return;
            }

            Sprite target = stand;
            if (gaitBlend > 0.3f)
            {
                Vector2 gait = style.GetGait(squad.Type);
                float phase = GaitPhase(index, local);
                int frame = math.sin(Time.time * gait.x * 2f * math.PI + phase) >= 0f ? 0 : 1;
                Sprite walk = style.GetWalkSprite(squad.Side, squad.Type, soldierRoles[index], frame);
                if (walk != null)
                {
                    target = walk;
                }
            }

            if (soldiers[index].sprite != target)
            {
                soldiers[index].sprite = target;
            }
        }

        private float GaitPhase(int index, float2 local)
        {
            switch (squad.Type)
            {
                case UnitType.Cavalry:
                    return -local.y * 1.3f + Scatter(index + 613).x * 0.4f;
                default:
                    return 0f;
            }
        }

        /// <summary>Выпад солдата в [0, 1]: быстрый удар вперёд и возврат, у каждого своя фаза.</summary>
        private float Thrust(int index)
        {
            float phase = Scatter(index + 907).x * math.PI;
            return math.pow(math.max(0f, math.sin(Time.time * style.ThrustRate * 2f * math.PI + phase)), 4f);
        }

        /// <summary>
        /// В рукопашной и при «Целься!» у солдата тело без оружия, а руки с ружьём (саблей) — отдельной фигуркой:
        /// в схватке она выбрасывается вперёд и возвращается, при прицеливании ружьё выносится вперёд и замирает
        /// (только у рядовых стреляющих шеренг). Иначе — обычная цельная фигурка.
        /// </summary>
        /// <returns>Солдат в позе с отдельными руками — кадр шага не нужен.</returns>
        private bool RenderArms(int index, float2 world, Quaternion rotation, Quaternion meleeRotation, float2 forward, bool shooter)
        {
            SpriteRenderer armsRenderer = arms[index];
            bool melee = meleeBlend > 0f;
            if (armsRenderer == null)
            {
                return melee;
            }

            bool aiming = !melee && aimBlend > 0f && shooter && soldierRoles[index] == SoldierRole.Private;
            bool posed = melee || aiming;
            if (posed && soldiers[index].sprite != bodySprites[index])
            {
                soldiers[index].sprite = bodySprites[index];
            }
            else if (!posed && soldiers[index].sprite == bodySprites[index])
            {
                soldiers[index].sprite = fullSprites[index];
            }

            if (armsRenderer.enabled != posed)
            {
                armsRenderer.enabled = posed;
            }

            if (!posed)
            {
                return false;
            }

            float2 reach = melee
                ? meleeDirection * (Thrust(index) * style.ThrustLength * math.smoothstep(0f, 1f, meleeBlend))
                : forward * (style.AimReach * math.smoothstep(0f, 1f, aimBlend));
            armsRenderer.transform.SetPositionAndRotation(new Vector3(world.x + reach.x, world.y + reach.y, 0f), melee ? meleeRotation : rotation);
            return true;
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

            // У конницы — только офицер в центре первой шеренги.
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
                soldiers[index].transform.localScale = Vector3.one * style.SoldierScale;

                fullSprites.Add(drawn);
                soldierRoles.Add(role);
                shownSlots.Add(new float2(float.NaN, float.NaN));
                if (drawn != null && style.TryGetMeleeSprites(squad.Side, squad.Type, role, out Sprite body, out Sprite armsSprite))
                {
                    bodySprites.Add(body);
                    SpriteRenderer armsRenderer = CreateRenderer($"Arms {index}", armsSprite, Color.white, order + 1);
                    armsRenderer.transform.localScale = Vector3.one * style.SoldierScale;
                    armsRenderer.enabled = false;
                    arms.Add(armsRenderer);
                }
                else
                {
                    bodySprites.Add(null);
                    arms.Add(null);
                }
            }

            while (soldiers.Count > alive)
            {
                int fallen = PickFallen();
                SpriteRenderer soldier = soldiers[fallen];
                if (casualties != null && soldier.enabled)
                {
                    // Павший остаётся на поле: ранение, падение, исчезновение.
                    soldier.sprite = fullSprites[fallen] != null ? fullSprites[fallen] : soldier.sprite;
                    casualties.Add(soldier);
                }
                else
                {
                    Destroy(soldier.gameObject);
                }

                soldiers.RemoveAt(fallen);
                if (arms[fallen] != null)
                {
                    Destroy(arms[fallen].gameObject);
                }

                arms.RemoveAt(fallen);
                bodySprites.RemoveAt(fallen);
                fullSprites.RemoveAt(fallen);
                soldierRoles.RemoveAt(fallen);
                shownSlots.RemoveAt(fallen);
            }
        }

        /// <summary>
        /// Кто падает: чаще первые шеренги (по ним бьют), знаменосец и офицер — последними.
        /// Выбор постоянный для отряда и его численности — без случайности кадра.
        /// </summary>
        private int PickFallen()
        {
            int count = soldiers.Count;
            int front = math.max(1, math.min(count, (count + 1) / 2));
            uint hash = (uint)(squad.Id * 7919 + count * 104729) * 2654435761u;
            int index = (int)(hash % (uint)front);
            for (int attempt = 0; attempt < count && soldierRoles[index] != SoldierRole.Private; attempt++)
            {
                index = (index + 1) % count;
            }

            return index;
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
