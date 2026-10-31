using System.Collections.Generic;
using Marchlands.Simulation.Buildings;
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

        // Какой слот текущего строя занимает солдат: после перестроения он остаётся там, куда пришёл, а не перебегает.
        private readonly List<int> slotOrder = new List<int>();
        private readonly List<float2> shownLocal = new List<float2>();
        private readonly List<SpriteRenderer> ringSegments = new List<SpriteRenderer>();

        // Пунктир маршрута дальнего марша у выделенного отряда: куда он пойдёт (к дороге, по ней, к цели).
        private readonly List<SpriteRenderer> routeDots = new List<SpriteRenderer>();

        private Squad squad;
        private Match match;
        private SquadSettings settings;
        private CombatSettings combat;
        private SquadViewStyle style;
        private SpriteRenderer selectionMarker;
        private SpriteRenderer targetMarker;
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
        private int slotsFileLimit;
        private bool slotsChanging;

        // У каждого солдата своя походка: 0 — стоит, 1 — идёт; меняется плавно, чтобы шаг не обрывался.
        // Шагает тот, кто сдвинулся с места, — и на марше, и когда строй разворачивается или перестраивается на месте.
        private readonly List<float> gaitBlends = new List<float>();
        private readonly List<float2> lastWorld = new List<float2>();

        // Фаза шага отряда (в циклах): растёт с пройденным путём, а не со временем — шаг в такт скорости.
        private float gaitPhase;
        private float gaitSpeed;
        private float frameTravel;
        private int frameMovers;

        // 0 — обычный строй, 1 — полная свалка рукопашной; меняется плавно.
        private float meleeBlend;

        // Штурм здания: строй рассыпается и обступает стены, часть лезет на них.
        private float assaultBlend;
        private float2 assaultCenter;
        private float2 assaultHalf = new float2(1f, 1f);
        private float2 assaultFrom = new float2(0f, -1f);
        private int frameRanks;

        internal System.Func<BuildingType, Vector2> BuildingSize { get; set; }
        private float aimBlend;
        private float2 meleeContact;
        private float2 meleeDirection = new float2(0f, 1f);

        // Колонна на марше изгибается по следу головы (см. ColumnTrail): 0 — прямоугольник, 1 — по следу.
        private readonly ColumnTrail trail = new ColumnTrail();
        private float bend;
        private float2 lastCenter;
        private bool hasLastCenter;
        private float lastFacing;

        // Сколько секунд колонна выпрямляется или изгибается заново (разворот на месте, перестроение).
        private const float BendBlendTime = 0.5f;

        // Центр сдвинулся за кадр меньше этого — колонна стоит (а если при этом поворачивается — разворот на месте), u.
        private const float StillEpsilon = 1e-4f;

        public Squad Squad => squad;

        // Пушка и передок у отряда-пушки.
        private SpriteRenderer gunRenderer;
        private SpriteRenderer limberRenderer;

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
            targetMarker = CreateRenderer("Gun Target", style.MarkerSprite, style.GunTargetColor, style.MarkerSortingOrder);
            targetMarker.enabled = false;
            moraleBackground = CreateRenderer("Morale", style.MarkerSprite, style.MoraleBarBackground, style.RingSortingOrder);
            moraleFill = CreateRenderer("Morale Fill", style.MarkerSprite, style.MoraleRoutingColor, style.RingSortingOrder + 1);
            CreateRing();
            if (squad.IsArtillery)
            {
                gunRenderer = CreateRenderer("Gun", style.GunSprite, Color.white, style.SoldierSortingOrder);
                gunRenderer.transform.localScale = Vector3.one * style.GunScale;
                limberRenderer = CreateRenderer("Limber", style.LimberSprite, Color.white, style.SoldierSortingOrder);
                limberRenderer.transform.localScale = Vector3.one * style.LimberScale;
            }

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
            UpdateAssault(position);
            UpdateAim();
            int shooters = VolleyMath.Shooters(squad.Formation, squad.Alive, settings);
            float2 forward = Angles.Direction(facing);
            Quaternion meleeRotation = Quaternion.Slerp(rotation,
                Quaternion.Euler(0f, 0f, Angles.Of(meleeDirection) + SpriteFacingOffset), meleeBlend);
            float2 spacing = GetSpacing();
            float regroup = 1f - math.exp(-style.RegroupSpeed * Time.deltaTime);
            float headY = UpdateTrail(position, facing, spacing, changing);
            frameTravel = 0f;
            frameMovers = 0;
            for (int i = 0; i < soldiers.Count; i++)
            {
                int slot = slotOrder[i];
                float2 local = changing ? math.lerp(fromSlots[slot], toSlots[transitionTargets[slot]], change) : fromSlots[slot];
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

                Quaternion soldierRotation = rotation;
                float2 soldierForward = forward;
                float gait = UpdateGait(i, ToWorld(local, position, facing, headY, ref soldierRotation, ref soldierForward));
                if (gait > 0f)
                {
                    local += GaitOffset(i, local, gait);
                }

                float2 world = ToWorld(local, position, facing, headY, ref soldierRotation, ref soldierForward);
                if (meleeBlend > 0f)
                {
                    world = ToMeleePosition(world, i);
                }

                Quaternion poseRotation = meleeRotation;
                float2 poseDirection = meleeDirection;
                float scale = 1f;
                if (assaultBlend > 0f)
                {
                    float2 storm = ToAssaultPosition(i, local, world, position, facing, out scale);
                    float blend = math.smoothstep(0f, 1f, assaultBlend);
                    world = math.lerp(world, storm, blend);
                    poseDirection = math.normalizesafe(assaultCenter - world, forward);
                    poseRotation = Quaternion.Slerp(soldierRotation, Quaternion.Euler(0f, 0f, Angles.Of(poseDirection) + SpriteFacingOffset), blend);
                    scale = math.lerp(1f, scale, blend);
                }

                bool fighting = meleeBlend > 0f || assaultBlend > 0f;
                soldiers[i].transform.SetPositionAndRotation(new Vector3(world.x, world.y, 0f), fighting ? poseRotation : soldierRotation);
                SetSoldierScale(i, scale);
                // Расчёт пушки ружей не вскидывает — наводит ствол.
                bool posed = RenderArms(i, world, soldierRotation, poseRotation, poseDirection, soldierForward, slot < shooters && !squad.IsArtillery);
                if (!posed)
                {
                    RenderStep(i, local, gait);
                }
            }

            AdvanceGaitPhase();
            RenderGun(position, facing, alpha);
            RenderRoute(position);
            RenderGunTarget(alpha);

            if (selectionMarker.enabled)
            {
                float2 half = FormationLayout.GetHalfExtents(squad, settings);
                if (squad.IsChangingFormation)
                {
                    half = math.lerp(half, FormationLayout.GetHalfExtents(squad.TargetFormation, squad.Alive, settings, squad.FileLimit), change);
                }

                half *= (float2)GetSpacing();

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

        /// <summary>
        /// Пушка перед расчётом: снятая с передка — стволом к врагу; на марше — передок впереди, пушка за ним стволом
        /// назад. Посередине снятия (взятия) на передок картинка меняется. Бегущие бросают пушку — её не видно.
        /// </summary>
        private void RenderGun(float2 position, float facing, float alpha)
        {
            if (gunRenderer == null)
            {
                return;
            }

            // Разбитый расчёт бросает пушку: она остаётся на месте и постепенно тает, как павшие.
            if (squad.IsRouting)
            {
                if (gunRenderer.enabled)
                {
                    AbandonGun();
                }

                return;
            }

            gunRenderer.enabled = true;
            float limberTime = match.Settings.guns.limberTime;
            // 0 — снята с передка (стволом к врагу перед расчётом), 1 — на передке (позади расчёта, её тянут).
            float towed = squad.IsLimbered ? 1f : 0f;
            if (squad.IsLimbering && limberTime > 0f)
            {
                float done = 1f - squad.LimberRemaining / limberTime;
                towed = squad.IsLimbered ? 1f - done : done;
            }

            float blend = math.smoothstep(0f, 1f, towed);
            limberRenderer.enabled = towed > 0.5f;

            float2 forward = Angles.Direction(facing);
            float halfDepth = FormationLayout.GetHalfExtents(squad, settings).y * GetSpacing().y;
            float2 front = position + forward * halfDepth;
            float2 back = position - forward * halfDepth;
            if (towed > 0f)
            {
                // На марше расчёт идёт впереди и тянет: за ним передок, за передком пушка стволом назад.
                // Снимая с передка, пушку обводят вокруг расчёта и ставят перед ним — без скачка.
                float2 limber = back - forward * (style.GunAhead * 0.5f);
                limberRenderer.transform.SetPositionAndRotation(new Vector3(limber.x, limber.y, 0f), Quaternion.Euler(0f, 0f, facing + SpriteFacingOffset));
                float2 towedAt = back - forward * style.LimberAhead;
                float2 firingAt = front + forward * style.GunAhead;
                float2 side = Angles.Right(facing) * (math.sin(blend * math.PI) * (halfDepth + style.GunAhead));
                float2 gun = math.lerp(firingAt, towedAt, blend) + side;
                gunRenderer.transform.SetPositionAndRotation(new Vector3(gun.x, gun.y, 0f), Quaternion.Euler(0f, 0f, facing + 180f * blend + SpriteFacingOffset));
            }
            else
            {
                // Откат после выстрела: ствол отскакивает назад и возвращается.
                float recoil = 0f;
                if (squad.FireState == FireState.Reloading)
                {
                    float since = match.Settings.guns.reloadTime - squad.FireTimer + alpha * SimulationTime.TickDuration;
                    recoil = since < RecoilTime ? math.sin(since / RecoilTime * math.PI) * RecoilDistance : 0f;
                }

                float2 gun = front + forward * (style.GunAhead - recoil);
                gunRenderer.transform.SetPositionAndRotation(new Vector3(gun.x, gun.y, 0f), Quaternion.Euler(0f, 0f, facing + SpriteFacingOffset));
            }
        }

        /// <summary>Пушка разбитого расчёта остаётся на поле отдельной фигурой и тает (см. <see cref="FadingSprite"/>).</summary>
        private void AbandonGun()
        {
            var abandoned = new GameObject("Abandoned Gun");
            abandoned.transform.SetParent(transform.parent, false);
            abandoned.transform.SetPositionAndRotation(gunRenderer.transform.position, gunRenderer.transform.rotation);
            abandoned.transform.localScale = gunRenderer.transform.lossyScale;
            var copy = abandoned.AddComponent<SpriteRenderer>();
            copy.sprite = gunRenderer.sprite;
            copy.color = gunRenderer.color * new Color(style.AbandonedGunDarkening, style.AbandonedGunDarkening, style.AbandonedGunDarkening, 1f);
            copy.sortingOrder = gunRenderer.sortingOrder;
            abandoned.AddComponent<FadingSprite>().Initialize(copy, style.AbandonedGunLieTime, style.AbandonedGunFadeTime);
            gunRenderer.enabled = false;
            limberRenderer.enabled = false;
        }

        private const float RecoilTime = 0.6f;
        private const float RecoilDistance = 0.5f;

        /// <summary>
        /// Ведёт след головы колонны и решает, насколько колонна изгибается по нему. Изгиб — только у колонны на марше;
        /// при развороте на месте, перестроении, бегстве и в рукопашной колонна плавно выпрямляется, и след начинается
        /// заново прямо за ней. Возвращает, где голова в локальных координатах (y фронта).
        /// </summary>
        private float UpdateTrail(float2 position, float facing, float2 spacing, bool changing)
        {
            float2 half = FormationLayout.GetHalfExtents(squad, settings) * spacing;
            float2 forward = Angles.Direction(facing);
            float2 head = position + forward * half.y;
            float length = half.y * 2f;

            bool moving = hasLastCenter && math.distancesq(position, lastCenter) > StillEpsilon * StillEpsilon;
            bool pivoting = hasLastCenter && !moving && math.abs(Angles.Delta(lastFacing, facing)) > 1e-3f;
            lastCenter = position;
            lastFacing = facing;
            hasLastCenter = true;

            bool column = squad.Formation == Formation.Column && !changing && !squad.IsRouting && meleeBlend <= 0f && assaultBlend <= 0f;
            float target = column && !pivoting ? 1f : 0f;
            bend = Mathf.MoveTowards(bend, target, Time.deltaTime / BendBlendTime);

            // Отряд, появившийся сразу в колонне, ещё без следа — след прямо за ним.
            if (bend <= 0f || !trail.IsValid)
            {
                trail.Straighten(head, facing, length);
            }
            else if (moving)
            {
                trail.Advance(head, length + GetSpacing().y * settings.rankSpacing * 2f);
            }

            return half.y;
        }

        /// <summary>
        /// Мировая позиция солдата с местом <paramref name="local"/>: в прямоугольнике строя или, у изогнутой колонны,
        /// на следе головы — на своём расстоянии назад, сдвинутым вбок по нормали к следу; фигурка смотрит по следу.
        /// </summary>
        private float2 ToWorld(float2 local, float2 position, float facing, float headY, ref Quaternion rotation, ref float2 forward)
        {
            float2 rigid = FormationLayout.LocalToWorld(local, position, facing);
            if (bend <= 0f)
            {
                return rigid;
            }

            trail.Sample(math.max(0f, headY - local.y), out float2 point, out float2 direction);
            float2 right = new float2(direction.y, -direction.x);
            float2 bent = point + right * local.x;
            float blend = math.smoothstep(0f, 1f, bend);
            float angle = Angles.Of(direction);
            rotation = Quaternion.Slerp(rotation, Quaternion.Euler(0f, 0f, angle + SpriteFacingOffset), blend);
            forward = math.normalizesafe(math.lerp(forward, direction, blend), forward);
            return math.lerp(rigid, bent, blend);
        }

        /// <summary>Пунктир оставшегося маршрута от отряда через точки пути к цели — только у выделенного.</summary>
        private void RenderRoute(float2 position)
        {
            int used = 0;
            if (selectionMarker.enabled && !squad.IsRouting && style.RouteDotSpacing > 0f && CollectRoute(position))
            {
                float2 from = position;
                float carry = style.RouteDotSpacing * 0.5f;
                for (int i = 0; i < routePoints.Count && used < style.RouteDotsMax; i++)
                {
                    float2 to = routePoints[i];
                    float length = math.distance(from, to);
                    float along = carry;
                    while (along < length && used < style.RouteDotsMax)
                    {
                        float2 dot = math.lerp(from, to, along / length);
                        if (used == routeDots.Count)
                        {
                            SpriteRenderer renderer = CreateRenderer("Route Dot", style.MarkerSprite, style.RouteDotColor, style.MarkerSortingOrder);
                            renderer.transform.localScale = Vector3.one * style.RouteDotSize;
                            routeDots.Add(renderer);
                        }

                        routeDots[used].enabled = true;
                        routeDots[used].transform.SetPositionAndRotation(new Vector3(dot.x, dot.y, 0f), Quaternion.Euler(0f, 0f, 45f));
                        used++;
                        along += style.RouteDotSpacing;
                    }

                    carry = along - length;
                    from = to;
                }
            }

            for (int i = used; i < routeDots.Count; i++)
            {
                routeDots[i].enabled = false;
            }
        }

        /// <summary>
        /// Точки пути выделенного отряда: дальний марш — по маршруту; короткий переход — прямо к цели, а если по пути
        /// река — через переправу. Разворот на месте пути не рисует.
        /// </summary>
        private bool CollectRoute(float2 position)
        {
            routePoints.Clear();
            if (squad.HasRoute)
            {
                for (int i = squad.CurrentRouteIndex; i < squad.RouteWaypoints.Count; i++)
                {
                    routePoints.Add(squad.RouteWaypoints[i]);
                }

                return true;
            }

            if (squad.Order.Type != SquadOrderType.Move || math.distancesq(position, squad.Order.Target) < RouteMinDistance * RouteMinDistance)
            {
                return false;
            }

            float2 from = position;
            float2 target = squad.Order.Target;
            for (int i = 0; i < RouteMaxDetours && match.Terrain != null; i++)
            {
                float2 next = match.Terrain.Steer(from, target);
                if (math.all(next == target) || math.distancesq(next, from) < RouteMinDistance * RouteMinDistance)
                {
                    break;
                }

                routePoints.Add(next);
                from = next;
            }

            routePoints.Add(target);
            return true;
        }

        private readonly List<float2> routePoints = new List<float2>();

        // Ближе — путь не рисуется (это разворот на месте), u; и сколько переправ на пути показывать.
        private const float RouteMinDistance = 0.3f;
        private const int RouteMaxDetours = 4;

        /// <summary>
        /// Разрядка строя на экране. Сжатый на мосту строй не шире, чем считает симуляция (конница шире пехоты
        /// на экране, но на настиле жмётся плотнее, а не свешивается над водой).
        /// </summary>
        private float2 GetSpacing()
        {
            float2 spacing = style.GetSpacing(squad.Type);
            if (squad.FileLimit > 0)
            {
                spacing.x = math.min(spacing.x, 1f);
            }

            return spacing;
        }

        /// <summary>У выбранной пушки подсвечена её цель: отряд или здание, по которому она бьёт или будет бить.</summary>
        private void RenderGunTarget(float alpha)
        {
            Squad targetSquad = null;
            Building targetBuilding = null;
            bool shown = selectionMarker.enabled && squad.IsArtillery && !squad.IsRouting
                && GunTargeting.TryGetTarget(match, squad, out targetSquad, out targetBuilding);
            if (targetMarker.enabled != shown)
            {
                targetMarker.enabled = shown;
            }

            if (!shown)
            {
                return;
            }

            float2 center;
            float2 half;
            float angle = 0f;
            if (targetSquad != null)
            {
                center = math.lerp(targetSquad.PreviousPosition, targetSquad.Position, alpha);
                half = FormationLayout.GetHalfExtents(targetSquad, settings) * (float2)style.GetSpacing(targetSquad.Type);
                angle = targetSquad.Facing + SpriteFacingOffset;
            }
            else
            {
                center = targetBuilding.Position;
                Vector2 size = BuildingSize != null ? BuildingSize(targetBuilding.Type) : new Vector2(2f, 2f);
                half = new float2(size.x, size.y) * 0.5f;
            }

            Color color = style.GunTargetColor;
            color.a *= 0.65f + 0.35f * math.sin(Time.time * GunTargetPulseRate);
            targetMarker.color = color;
            targetMarker.transform.SetPositionAndRotation(new Vector3(center.x, center.y, 0f), Quaternion.Euler(0f, 0f, angle));
            targetMarker.transform.localScale = new Vector3((half.x + style.SelectionPadding) * 2f, (half.y + style.SelectionPadding) * 2f, 1f);
        }

        // Как быстро мерцает подсветка цели пушки, рад/с.
        private const float GunTargetPulseRate = 5f;

        /// <summary>Расстояние от центра отряда до его тыла.</summary>
        private float GetRearDistance()
        {
            float halfDepth = FormationLayout.GetHalfExtents(squad, settings).y * GetSpacing().y;
            if (squad.IsRouting)
            {
                return halfDepth * style.RoutSpread + style.RoutScatter;
            }

            // Пушку на передке тянут позади расчёта — индикаторы за ней, а не поверх.
            return squad.IsArtillery && squad.IsLimbered ? halfDepth + style.LimberAhead + style.GunAhead : halfDepth;
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

        /// <summary>«Целься!»: стреляющие вскидывают ружья, пока отряд прицеливается, и опускают после залпа.</summary>
        private void UpdateAim()
        {
            bool aiming = squad.FireState == FireState.Aiming && !squad.IsRouting;
            float time = aiming ? style.AimRaiseTime : style.AimLowerTime;
            float step = time > 0f ? Time.deltaTime / time : 1f;
            aimBlend = aiming ? math.min(1f, aimBlend + step) : math.max(0f, aimBlend - step);
        }

        /// <summary>
        /// Походка солдата: идёт, если его место на земле сдвигается быстрее stepMinSpeed — на марше, при развороте
        /// строя (фланги проходят дугу, середина почти стоит), при перестроении и смыкании после потерь.
        /// </summary>
        private float UpdateGait(int index, float2 world)
        {
            float delta = Time.deltaTime;
            float blend = gaitBlends[index];
            if (delta <= 0f)
            {
                return blend;
            }

            float2 last = lastWorld[index];
            lastWorld[index] = world;
            if (!float.IsNaN(last.x))
            {
                frameTravel += math.distance(world, last);
                frameMovers++;
            }

            bool moving = !float.IsNaN(last.x) && !squad.IsInMelee
                && math.distancesq(world, last) > style.StepMinSpeed * style.StepMinSpeed * delta * delta;
            float step = style.GaitBlendTime > 0f ? delta / style.GaitBlendTime : 1f;
            blend = moving ? math.min(1f, blend + step) : math.max(0f, blend - step);
            gaitBlends[index] = blend;
            return blend;
        }

        /// <summary>
        /// Фаза шага растёт на пройденный солдатами путь (в среднем за кадр), делённый на длину шага: быстрее идут —
        /// чаще шагают, стоят — шаг замирает. Скорость сглажена, чтобы шаг не дёргался от неровных кадров.
        /// </summary>
        private void AdvanceGaitPhase()
        {
            float delta = Time.deltaTime;
            if (delta <= 0f || frameMovers == 0)
            {
                return;
            }

            float speed = frameTravel / frameMovers / delta;
            gaitSpeed = math.lerp(gaitSpeed, speed, 1f - math.exp(-GaitSpeedSmoothing * delta));
            gaitPhase += gaitSpeed / style.GetStride(squad.Type, gaitSpeed) * delta;
            gaitPhase -= math.floor(gaitPhase);
        }

        // Насколько быстро сглаженная скорость шага догоняет настоящую, 1/s.
        private const float GaitSpeedSmoothing = 8f;

        /// <summary>
        /// Смещение солдата в строю от походки (вид сверху: покачивание с ноги на ногу и рывки вперёд).
        /// Пехота марширует в ногу — у всех одна фаза; конница скачет —
        /// рывками вперёд-назад, по шеренгам с запаздыванием.
        /// </summary>
        private float2 GaitOffset(int index, float2 local, float blend)
        {
            Vector2 gait = style.GetGait(squad.Type);
            float time = gaitPhase * 2f * math.PI;
            float amplitude = gait.y * math.smoothstep(0f, 1f, blend);
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
        /// Штурм: отряд с приказом на штурм дошёл до здания и бьёт его — строй рассыпается вокруг стен;
        /// приказ кончился (здание пало, отозвали) или началась рукопашная — собирается обратно.
        /// </summary>
        private void UpdateAssault(float2 position)
        {
            Building target = squad.Order.Type == SquadOrderType.Assault ? match.FindBuilding(squad.Order.TargetBuildingId) : null;
            bool storming = target != null && !target.IsDestroyed && !squad.IsRouting && meleeBlend <= 0f
                && math.distance(position, target.Position) <= match.Settings.buildings.assaultRange + AssaultReachSlack;
            float step = style.AssaultBlendTime > 0f ? Time.deltaTime / style.AssaultBlendTime : 1f;
            if (storming)
            {
                assaultCenter = target.Position;
                Vector2 size = BuildingSize != null ? BuildingSize(target.Type) : new Vector2(2f, 2f);
                assaultHalf = new float2(size.x, size.y) * 0.5f;
                float2 away = position - assaultCenter;
                if (math.lengthsq(away) > 1e-6f)
                {
                    assaultFrom = math.normalize(away);
                }

                assaultBlend = math.min(1f, assaultBlend + step);
            }
            else
            {
                assaultBlend = math.max(0f, assaultBlend - step);
            }

            FormationLayout.GetGrid(squad, settings, out int _, out frameRanks);
        }

        // Отряд встаёт ровно на assaultRange; на сколько дальше ещё считаем, что он у стен, u.
        private const float AssaultReachSlack = 0.3f;

        /// <summary>
        /// Место солдата на штурме: по дуге вокруг здания со своей стороны (правый фланг строя — справа), первая
        /// шеренга у стен, задние — дальше. Первая волна толкается и колет стену, часть лезет на неё: заходит
        /// на здание, наверху крупнее (ближе к зрителю), и срывается обратно.
        /// </summary>
        private float2 ToAssaultPosition(int index, float2 local, float2 formationWorld, float2 position, float facing, out float scale)
        {
            scale = 1f;
            float2 half = FormationLayout.GetHalfExtents(squad, settings) * GetSpacing();
            // Место на дуге — по тому, где солдат стоит сейчас, если смотреть со здания: кто левее, тот и встаёт левее.
            // Так никто не перебегает на другой край — к своему участку стены каждому ближе всех.
            float2 across = new float2(-assaultFrom.y, assaultFrom.x);
            float lateralExtent = math.abs(half.x * math.dot(Angles.Right(facing), across)) + math.abs(half.y * math.dot(Angles.Direction(facing), across));
            float spread = lateralExtent > 1e-3f ? math.clamp(math.dot(formationWorld - position, across) / lateralExtent, -1f, 1f) : 0f;
            float rankStep = settings.rankSpacing * GetSpacing().y;
            int rank = rankStep > 1e-3f ? math.clamp((int)math.round((half.y - local.y) / rankStep - 0.5f), 0, math.max(0, frameRanks - 1)) : 0;

            float angle = Angles.Of(assaultFrom) + spread * style.AssaultArc * 0.5f + Scatter(index + 311).x * 2f;
            float2 direction = Angles.Direction(angle);
            float edge = math.min(assaultHalf.x / math.max(math.abs(direction.x), 1e-3f), assaultHalf.y / math.max(math.abs(direction.y), 1e-3f));
            float radius = edge + style.AssaultStandOff + rank * style.AssaultRankGap + Scatter(index + 421).y * 0.15f;
            float2 tangent = new float2(-direction.y, direction.x);
            float sway = math.sin(Time.time * 2.3f + Scatter(index + 977).x * 6f) * style.AssaultSway;

            bool climber = rank == 0 && (Scatter(index + 733).x + 1f) * 0.5f < style.AssaultClimbShare;
            if (climber && style.AssaultClimbPeriod > 0f)
            {
                float phase = Time.time / style.AssaultClimbPeriod + Scatter(index + 857).y;
                phase -= math.floor(phase);
                // Карабкается, держится наверху, срывается вниз.
                float up = math.smoothstep(0f, 0.45f, phase) * (1f - math.smoothstep(0.75f, 0.9f, phase));
                radius = math.lerp(radius, edge - style.AssaultClimbDepth, up);
                scale = 1f + style.AssaultClimbScale * up;
                sway *= 1f - up;
            }
            else
            {
                // Подаётся к стене за выпадом.
                radius -= Thrust(index) * style.ThrustLength * 0.4f;
            }

            return assaultCenter + direction * radius + tangent * sway;
        }

        private void SetSoldierScale(int index, float scale)
        {
            Vector3 target = Vector3.one * (style.SoldierScale * scale);
            if (soldiers[index].transform.localScale != target)
            {
                soldiers[index].transform.localScale = target;
                if (arms[index] != null)
                {
                    arms[index].transform.localScale = target;
                }
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
        private void RenderStep(int index, float2 local, float blend)
        {
            Sprite stand = fullSprites[index];
            if (stand == null)
            {
                return;
            }

            Sprite target = stand;
            if (blend > 0.3f)
            {
                Vector2 gait = style.GetGait(squad.Type);
                float phase = GaitPhase(index, local);
                int frame = math.sin(gaitPhase * 2f * math.PI + phase) >= 0f ? 0 : 1;
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
        private bool RenderArms(int index, float2 world, Quaternion rotation, Quaternion meleeRotation, float2 meleeDir, float2 forward, bool shooter)
        {
            SpriteRenderer armsRenderer = arms[index];
            float fightBlend = math.max(meleeBlend, assaultBlend);
            bool melee = fightBlend > 0f;
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
                ? meleeDir * (Thrust(index) * style.ThrustLength * math.smoothstep(0f, 1f, fightBlend))
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
                    // В рукопашной перезарядка стоит — кольцо не ползёт.
                    float remaining = math.max(0f, squad.FireTimer - (squad.IsInMelee ? 0f : alpha * SimulationTime.TickDuration));
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
            if (slotsFormation == squad.Formation && slotsTarget == squad.TargetFormation && slotsFileLimit == squad.FileLimit
                && slotsAlive == squad.Alive && slotsChanging == changing && fromSlots.Count == squad.Alive)
            {
                return;
            }

            // Перестроение закончилось или повернуло назад: прежняя цель стала текущим строем, и солдат
            // занимает тот слот, к которому шёл.
            // Строй сжался на мосту (или раздался обратно) — солдаты занимают новые места по порядку от того, где стоят.
            bool sameSoldiers = slotsAlive == squad.Alive && slotOrder.Count == squad.Alive && slotsFileLimit == squad.FileLimit;
            if (sameSoldiers && slotsChanging && squad.Formation == slotsTarget && squad.Formation != slotsFormation)
            {
                for (int i = 0; i < slotOrder.Count; i++)
                {
                    slotOrder[i] = transitionTargets[slotOrder[i]];
                }
            }

            slotsFormation = squad.Formation;
            slotsTarget = squad.TargetFormation;
            slotsAlive = squad.Alive;
            slotsFileLimit = squad.FileLimit;
            slotsChanging = changing;

            FormationLayout.GetLocalSlots(squad.Formation, squad.Alive, settings, fromSlots, squad.FileLimit);
            if (!sameSoldiers)
            {
                AssignSlotsByPosition();
            }

            if (changing)
            {
                FormationLayout.GetLocalSlots(squad.TargetFormation, squad.Alive, settings, toSlots, squad.FileLimit);
                FormationLayout.MatchTransitionSlots(fromSlots, toSlots, transitionTargets);
            }
        }

        /// <summary>
        /// Численность сменилась: солдаты занимают новые слоты по порядку слева направо от того места, где стоят,
        /// чтобы не перебегать через весь строй. Новичок, ещё не стоявший в строю, считается стоящим в слоте своего номера.
        /// </summary>
        private void AssignSlotsByPosition()
        {
            float2 spacing = GetSpacing();
            shownLocal.Clear();
            for (int i = 0; i < soldiers.Count; i++)
            {
                float2 shown = shownSlots[i];
                shownLocal.Add(float.IsNaN(shown.x) || float.IsNaN(shown.y) ? fromSlots[i] : shown / spacing);
            }

            FormationLayout.MatchTransitionSlots(shownLocal, fromSlots, slotOrder);
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
                gaitBlends.Add(0f);
                lastWorld.Add(new float2(float.NaN, float.NaN));
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
                gaitBlends.RemoveAt(fallen);
                lastWorld.RemoveAt(fallen);
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
