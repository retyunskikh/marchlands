using System;
using System.Collections.Generic;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Melee;
using Marchlands.Simulation.Squads;
using Marchlands.View;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

namespace Marchlands.Input
{
    /// <summary>
    /// Тач-управление боем (раздел 4 docs/design.md): выделение отрядов и зданий, приказы движения, постановка
    /// казармы, камера. Состояние симуляции только читает, приказы отдаёт командами.
    /// </summary>
    public sealed class BattleInputController : MonoBehaviour
    {
        [SerializeField] private InputConfig config;
        [SerializeField] private CameraController cameraController;
        [SerializeField] private OrderPreviewView orderPreview;
        [SerializeField] private RotationRingView rotationRing;
        [SerializeField] private BuildingViewRegistry buildingViews;

        private Match match;
        private Side playerSide;
        private IGestures gestures;
        private Squad selected;

        // Все выделенные отряды; первый — главный (selected): по нему кольцо поворота, сектор огня и карточка.
        private readonly List<Squad> group = new List<Squad>();

        private bool isDraggingOrder;
        private bool isRotating;

        // Протяжка приказа: палец задержали — точка закреплена, дальше палец задаёт фронт в ней.
        private bool destinationLocked;
        private float2 lockedDestination;
        private float lockedFacing;
        private Vector2 lastMoveScreen;
        private double lastMoveTime;
        private Vector2 lastDragPosition;

        private bool hasPendingTap;
        private double pendingTapTime;
        private Vector2 pendingTapPosition;

        // Первый тап по своему отряду при выделенном другом: второй тап по нему же отправит прежний отряд к этому.
        private Squad squadTapFrom;
        private Squad squadTapOn;
        private double squadTapTime;
        private Vector2 squadTapPosition;

        private bool isChargeTargeting;

        private Building selectedBuilding;

        // Выделенная группа своей толпы рекрутов (род войск) — не отряд: её можно только распустить.
        private UnitType? selectedCamp;
        private bool isPlacing;
        private BuildingType placementType;
        private float2? placementSite;
        private bool lastPlacementConfirmable;
        private SiteProblem lastPlacementProblem;

        /// <summary>Выделенный отряд изменился; null — выделение снято.</summary>
        public event Action<Squad> SelectionChanged;

        /// <summary>Выделенное здание изменилось; null — выделение снято.</summary>
        public event Action<Building> BuildingSelectionChanged;

        /// <summary>Выделена группа своей толпы рекрутов этого рода войск; null — выделение снято.</summary>
        public event Action<UnitType?> CampSelectionChanged;

        /// <summary>Режим постановки здания: включён ли он, выбрано ли место и можно ли там поставить здание.</summary>
        /// <summary>Идёт выбор места; место выбрано; почему там нельзя строить (None — можно).</summary>
        public event Action<bool, bool, SiteProblem> PlacementChanged;

        /// <summary>Включился или выключился выбор цели для «В штыки».</summary>
        public event Action<bool> ChargeTargetingChanged;

        public Squad Selected => selected;

        /// <summary>Выделенные отряды (первый — главный); пусто — ничего не выделено.</summary>
        public IReadOnlyList<Squad> SelectedGroup => group;

        /// <summary>Набор выделенных отрядов изменился (первый — главный).</summary>
        public event Action<IReadOnlyList<Squad>> GroupChanged;

        /// <summary>Управление мышью и клавиатурой (ПК, редактор), а не пальцами.</summary>
        public static bool UsesMouse
        {
            get { return Platform.IsDesktop; }
        }

        public bool IsPlacing => isPlacing;

        public bool IsChargeTargeting => isChargeTargeting;

        public Match Match => match;

        public Side PlayerSide => playerSide;

        public CameraController CameraRig => cameraController;

        public InputConfig Config => config;

        /// <summary>Границы карты для камеры (из сценария боя).</summary>
        public void SetMapBounds(Vector2 center, Vector2 size)
        {
            cameraController.SetMapBounds(center, size);
        }

        public void Initialize(Match match, Side playerSide)
        {
            this.match = match;
            this.playerSide = playerSide;
            cameraController.Initialize(config);
            rotationRing.Initialize(match);
        }

        /// <summary>«Казарма» или «Конюшня» нажата: тап по земле выбирает место, затем «Поставить» или «Отмена».</summary>
        public void BeginPlacement(BuildingType type)
        {
            if (match.GetHeadquarters(playerSide) == null)
            {
                return;
            }

            Select(null);
            SelectBuilding(null);
            SelectCamp(null);
            isPlacing = true;
            placementType = type;
            placementSite = null;
            RefreshPlacement(force: true);
        }

        public void ConfirmPlacement()
        {
            if (isPlacing && placementSite.HasValue && BuildRules.CanBuild(match, playerSide, placementType, placementSite.Value))
            {
                match.Enqueue(placementType == BuildingType.Stable
                    ? new BuildStableCommand(playerSide, placementSite.Value)
                    : placementType == BuildingType.ArtilleryDepot
                        ? new BuildDepotCommand(playerSide, placementSite.Value)
                        : (ICommand)new BuildBarracksCommand(playerSide, placementSite.Value));
            }

            CancelPlacement();
        }

        public void CancelPlacement()
        {
            if (!isPlacing)
            {
                return;
            }

            isPlacing = false;
            placementSite = null;
            buildingViews.HidePlacement();
            RefreshPlacement(force: true);
        }

        /// <summary>«В штыки» нажата: следующий тап по вражескому отряду отдаст приказ, тап мимо — отменит.</summary>
        /// <summary>Выбор цели тапом: для отряда — кого атаковать в штыки, для пушки — по кому бить.</summary>
        public void BeginChargeTargeting()
        {
            SetChargeTargeting(CanPickTarget(selected));
        }

        private static bool CanPickTarget(Squad squad)
        {
            return squad != null && (squad.IsArtillery ? squad.CanReceiveOrders : ChargeRules.CanCharge(squad));
        }

        private void OnEnable()
        {
            // Создаём здесь, а не в Awake: после перекомпиляции в Play Mode Unity вызывает OnEnable без Awake.
            // На ПК (и в редакторе) левая кнопка мыши — как палец; на телефоне — касания.
            gestures ??= UsesMouse ? new MouseGestures(config) : (IGestures)new TouchGestures(config);

            EnhancedTouchSupport.Enable();
            gestures.Tapped += OnTapped;
            gestures.DragStarted += OnDragStarted;
            gestures.DragUpdated += OnDragUpdated;
            gestures.DragEnded += OnDragEnded;
            gestures.DragCancelled += OnDragCancelled;
            gestures.Pinched += OnPinched;
        }

        private void OnDisable()
        {
            gestures.Tapped -= OnTapped;
            gestures.DragStarted -= OnDragStarted;
            gestures.DragUpdated -= OnDragUpdated;
            gestures.DragEnded -= OnDragEnded;
            gestures.DragCancelled -= OnDragCancelled;
            gestures.Pinched -= OnPinched;
            EnhancedTouchSupport.Disable();
        }

        private void Update()
        {
            if (match == null)
            {
                return;
            }

            // Выделенный отряд мог погибнуть; из группы выбывают погибшие и бегущие.
            if (selected != null && match.FindSquad(selected.Id) == null)
            {
                hasPendingTap = false;
                Select(null);
            }

            if (group.RemoveAll(s => match.FindSquad(s.Id) == null || s.IsRouting) > 0)
            {
                SetGroup(new List<Squad>(group));
            }

            // Выделенное здание могли разрушить.
            if (selectedBuilding != null && match.FindBuilding(selectedBuilding.Id) == null)
            {
                SelectBuilding(null);
            }

            // Выделенную группу толпы могли распустить, собрать в отряд или перебить.
            if (selectedCamp.HasValue && match.GetPlayer(playerSide).GetReserve(selectedCamp.Value) <= 0)
            {
                SelectCamp(null);
            }

            if (isChargeTargeting && !CanPickTarget(selected))
            {
                SetChargeTargeting(false);
            }

            gestures.Update();
            UpdateMouseWheel();
            RefreshPlacement(force: false);

            // Одиночный тап по земле снимает выделение, только если за ним не последовал второй тап.
            if (hasPendingTap && Time.realtimeSinceStartupAsDouble - pendingTapTime > config.DoubleTapMaxInterval)
            {
                hasPendingTap = false;
                Select(null);
            }
        }

        private void OnTapped(Vector2 screenPosition)
        {
            float2 world = cameraController.ScreenToWorld(screenPosition);
            if (isPlacing)
            {
                placementSite = world;
                RefreshPlacement(force: true);
                return;
            }

            if (isChargeTargeting)
            {
                HandleChargeTargetTap(world);
                return;
            }

            Squad hit = FindSquadAt(world, own: true);
            if (hit != null)
            {
                hasPendingTap = false;
                HandleOwnSquadTap(hit, screenPosition);
                return;
            }

            // Тап по врагу при выбранной пушке — указать ей цель (отряд или здание).
            if (selected != null && selected.IsArtillery)
            {
                Squad enemy = FindSquadAt(world, own: false);
                if (enemy != null)
                {
                    hasPendingTap = false;
                    OrderAttack(enemy, gunsOnly: true);
                    return;
                }

                Building enemyTarget = FindBuildingAt(world, own: false);
                if (enemyTarget != null)
                {
                    hasPendingTap = false;
                    OrderAssault(enemyTarget, gunsOnly: true);
                    return;
                }
            }

            // Тап по вражескому зданию при выбранном отряде — сразу штурм, без кнопки «В штыки».
            if (selected != null && ChargeRules.CanCharge(selected))
            {
                Building assaulted = FindBuildingAt(world, own: false);
                if (assaulted != null)
                {
                    hasPendingTap = false;
                    OrderAssault(assaulted);
                    return;
                }
            }

            // Тап по своей толпе у казармы или конюшни выделяет группу этого рода войск — её можно только распустить.
            UnitType? camp = FindOwnCampTypeAt(world);
            if (camp.HasValue)
            {
                hasPendingTap = false;
                SelectCamp(camp);
                return;
            }

            // Вражеское здание показывает своё здоровье и часовых, когда отряд не выделен (иначе тап — часть приказа).
            Building building = FindBuildingAt(world, own: true)
                ?? (selected == null ? FindBuildingAt(world, own: false) : null);
            if (building != null)
            {
                hasPendingTap = false;
                SelectBuilding(building);
                return;
            }

            if (selectedBuilding != null || selectedCamp.HasValue)
            {
                SelectBuilding(null);
                SelectCamp(null);
                return;
            }

            if (selected == null)
            {
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            bool isDoubleTap = hasPendingTap
                && now - pendingTapTime <= config.DoubleTapMaxInterval
                && Vector2.Distance(screenPosition, pendingTapPosition) <= ScreenUnits.DpToPixels(config.DoubleTapMaxDistance);

            if (isDoubleTap)
            {
                hasPendingTap = false;
                OrderMove(world, null);
                return;
            }

            hasPendingTap = true;
            pendingTapTime = now;
            pendingTapPosition = screenPosition;
        }

        /// <summary>
        /// Тап по своему отряду выделяет его. Двойной тап по нему при выделенном другом отряде — прежний отряд
        /// идёт к этому и встаёт за ним, в затылок, тем же фронтом; выделенным остаётся тот, по кому тапали.
        /// </summary>
        private void HandleOwnSquadTap(Squad hit, Vector2 screenPosition)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            bool isDoubleTap = squadTapOn == hit
                && now - squadTapTime <= config.DoubleTapMaxInterval
                && Vector2.Distance(screenPosition, squadTapPosition) <= ScreenUnits.DpToPixels(config.DoubleTapMaxDistance);
            if (isDoubleTap && UsesMouse)
            {
                // Мышью двойной клик по отряду — все свои отряды этого рода войск на экране.
                squadTapOn = null;
                squadTapFrom = null;
                SelectSameTypeOnScreen(hit);
                return;
            }

            if (isDoubleTap)
            {
                Squad mover = squadTapFrom;
                squadTapOn = null;
                squadTapFrom = null;
                if (mover != null && mover != hit && match.FindSquad(mover.Id) != null && mover.CanReceiveOrders)
                {
                    SquadSettings settings = match.Settings.squads;
                    float depth = FormationLayout.GetHalfExtents(hit, settings).y
                        + FormationLayout.GetHalfExtents(mover.TargetFormation, mover.Alive, settings).y + settings.rankSpacing;
                    float2 behind = hit.Position - Angles.Direction(hit.Facing) * depth;
                    match.Enqueue(new MoveCommand(mover.Id, behind, hit.Facing, autoRoute: true));
                    foreach (Squad other in group)
                    {
                        if (other != mover && other != hit && other.CanReceiveOrders)
                        {
                            match.Enqueue(new MoveCommand(other.Id, behind - Angles.Direction(hit.Facing) * depth, hit.Facing, autoRoute: true));
                        }
                    }
                }

                Select(hit);
                return;
            }

            squadTapFrom = selected != hit ? selected : null;
            squadTapOn = hit;
            squadTapTime = now;
            squadTapPosition = screenPosition;
            Select(hit);
        }

        /// <summary>
        /// Протяжка от кольца вокруг выделенного отряда — поворот; от самого отряда — приказ движения;
        /// иначе — прокрутка камеры.
        /// </summary>
        private void OnDragStarted(Vector2 startScreen)
        {
            float2 world = cameraController.ScreenToWorld(startScreen);
            isRotating = selected != null && selected.CanReceiveOrders
                && rotationRing.IsOnRing(selected, world, ScreenToWorldDistance(startScreen, config.RotationRingGrab));
            isDraggingOrder = !isRotating && selected != null && FindSquadAt(world, own: true) == selected;
            lastDragPosition = startScreen;
            destinationLocked = false;
            lastMoveScreen = startScreen;
            lastMoveTime = Time.realtimeSinceStartupAsDouble;
            if (isDraggingOrder || isRotating)
            {
                hasPendingTap = false;
            }
        }

        /// <summary>Сколько u в мире занимают <paramref name="dp"/> на экране рядом с точкой.</summary>
        private float ScreenToWorldDistance(Vector2 screen, float dp)
        {
            float pixels = ScreenUnits.DpToPixels(dp);
            return math.distance(cameraController.ScreenToWorld(screen), cameraController.ScreenToWorld(screen + new Vector2(pixels, 0f)));
        }

        private float FacingTowards(Vector2 screen)
        {
            float2 toFinger = (float2)cameraController.ScreenToWorld(screen) - selected.Position;
            return math.lengthsq(toFinger) > 1e-6f ? Angles.Of(toFinger) : selected.Facing;
        }

        private void OnDragUpdated(Vector2 startScreen, Vector2 currentScreen)
        {
            if (isRotating)
            {
                float facing = FacingTowards(currentScreen);
                rotationRing.SetDraggedFacing(facing);
                orderPreview.Show(selected.Position, selected.Position, facing, selected.TargetFormation, selected.Alive, match.Settings.squads);
                return;
            }

            if (!isDraggingOrder)
            {
                cameraController.Pan(currentScreen - lastDragPosition);
                lastDragPosition = currentScreen;
                return;
            }

            float2 from = cameraController.ScreenToWorld(startScreen);
            float2 to = cameraController.ScreenToWorld(currentScreen);
            UpdateDestinationLock(startScreen, currentScreen, from, to);
            if (destinationLocked)
            {
                orderPreview.Show(from, lockedDestination, lockedFacing, selected.TargetFormation, selected.Alive, match.Settings.squads);
                return;
            }

            orderPreview.Show(from, to, Angles.Of(to - from), selected.TargetFormation, selected.Alive, match.Settings.squads);
        }

        /// <summary>
        /// Палец задержали на месте holdToAimTime — точка приказа закрепляется. Дальше направление от неё к пальцу
        /// задаёт фронт; пока палец у самой точки, фронт — по направлению протяжки.
        /// </summary>
        private void UpdateDestinationLock(Vector2 startScreen, Vector2 currentScreen, float2 from, float2 to)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (!destinationLocked)
            {
                if (Vector2.Distance(currentScreen, lastMoveScreen) > ScreenUnits.DpToPixels(config.HoldStillTolerance))
                {
                    lastMoveScreen = currentScreen;
                    lastMoveTime = now;
                }
                else if (now - lastMoveTime >= config.HoldToAimTime
                    && Vector2.Distance(currentScreen, startScreen) > ScreenUnits.DpToPixels(config.TapMaxMove))
                {
                    destinationLocked = true;
                    lockedDestination = to;
                    lockedFacing = Angles.Of(to - from);
                }

                return;
            }

            float2 toFinger = to - lockedDestination;
            if (math.length(toFinger) > ScreenToWorldDistance(currentScreen, config.HoldStillTolerance * 2f))
            {
                lockedFacing = Angles.Of(toFinger);
            }
        }

        private void OnDragEnded(Vector2 startScreen, Vector2 endScreen)
        {
            if (isRotating)
            {
                isRotating = false;
                rotationRing.SetDraggedFacing(null);
                orderPreview.Hide();
                // Поворот на месте — всей группы к одному фронту.
                float faced = FacingTowards(endScreen);
                foreach (Squad squad in group)
                {
                    match.Enqueue(new FaceCommand(squad.Id, faced));
                }

                return;
            }

            if (!isDraggingOrder)
            {
                return;
            }

            isDraggingOrder = false;
            orderPreview.Hide();

            if (destinationLocked)
            {
                destinationLocked = false;
                GroupMove.EnqueueAnchored(match, group, selected, lockedDestination, lockedFacing, selected.Facing, autoRoute: true);
                return;
            }

            float2 from = cameraController.ScreenToWorld(startScreen);
            float2 to = cameraController.ScreenToWorld(endScreen);
            GroupMove.EnqueueAnchored(match, group, selected, to, Angles.Of(to - from), selected.Facing, autoRoute: true);
        }

        private void OnDragCancelled()
        {
            isDraggingOrder = false;
            destinationLocked = false;
            isRotating = false;
            rotationRing.SetDraggedFacing(null);
            orderPreview.Hide();
        }

        private void OnPinched(Vector2 center, Vector2 centerDelta, float spread)
        {
            cameraController.Pan(centerDelta);
            cameraController.Zoom(center, spread);
        }

        private void UpdateMouseWheel()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f)
            {
                float spread = scroll > 0f ? config.WheelZoomFactor : 1f / config.WheelZoomFactor;
                cameraController.Zoom(mouse.position.ReadValue(), spread);
            }
        }

        private void HandleChargeTargetTap(float2 world)
        {
            SetChargeTargeting(false);
            Squad enemy = FindSquadAt(world, own: false);
            if (enemy != null)
            {
                OrderAttack(enemy);
                return;
            }

            // Тап по вражескому зданию — штурм в штыки (пушкам — цель).
            Building enemyBuilding = FindBuildingAt(world, own: false);
            if (enemyBuilding != null)
            {
                OrderAssault(enemyBuilding);
                return;
            }

            // Тап по своему отряду выделяет его, тап по земле просто отменяет выбор цели.
            Squad own = FindSquadAt(world, own: true);
            if (own != null)
            {
                Select(own);
            }
        }

        private void SetChargeTargeting(bool enabled)
        {
            if (isChargeTargeting == enabled)
            {
                return;
            }

            isChargeTargeting = enabled;
            ChargeTargetingChanged?.Invoke(enabled);
        }

        /// <summary>Перерисовывает предпросмотр и сообщает, если изменилось, можно ли ставить (золото тоже может измениться).</summary>
        private void RefreshPlacement(bool force)
        {
            SiteProblem problem = isPlacing && placementSite.HasValue
                ? BuildRules.GetSiteProblem(match, playerSide, placementType, placementSite.Value)
                : SiteProblem.None;
            bool confirmable = isPlacing && placementSite.HasValue && problem == SiteProblem.None;
            if (isPlacing)
            {
                Building headquarters = match.GetHeadquarters(playerSide);
                Vector2? site = placementSite.HasValue ? (Vector2)placementSite.Value : (Vector2?)null;
                bool shownValid = confirmable;
                // На ПК, пока место не выбрано кликом, здание показывается под курсором — сразу видно, можно ли там.
                if (!site.HasValue && UsesMouse && TryGetHoverSite(out float2 hover))
                {
                    site = hover;
                    shownValid = BuildRules.GetSiteProblem(match, playerSide, placementType, hover) == SiteProblem.None;
                }

                buildingViews.ShowPlacement(headquarters.Position, site, shownValid, placementType);
            }

            if (force || problem != lastPlacementProblem || confirmable != lastPlacementConfirmable)
            {
                lastPlacementConfirmable = confirmable;
                lastPlacementProblem = problem;
                PlacementChanged?.Invoke(isPlacing, placementSite.HasValue, problem);
            }
        }

        /// <summary>Точка карты под курсором мыши (не над интерфейсом).</summary>
        private bool TryGetHoverSite(out float2 site)
        {
            site = float2.zero;
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            Vector2 pointer = mouse.position.ReadValue();
            if (TouchGestures.IsOverUi(pointer, hoverUiHits))
            {
                return false;
            }

            site = cameraController.ScreenToWorld(pointer);
            return true;
        }

        private readonly List<UnityEngine.EventSystems.RaycastResult> hoverUiHits = new List<UnityEngine.EventSystems.RaycastResult>();

        /// <summary>
        /// Здание под точкой: внутри его видимого прямоугольника (с запасом в полпальца) или, для маленьких,
        /// ближе buildingTapRadius к центру. Из нескольких — ближайшее к точке.
        /// </summary>
        private Building FindBuildingAt(float2 world, bool own)
        {
            Building best = null;
            float bestDistance = float.MaxValue;
            float pad = cameraController.PixelsToWorld(ScreenUnits.DpToPixels(config.MinTouchTarget)) * 0.25f;
            foreach (Building building in match.Buildings)
            {
                // Вражеское здание в тумане войны не выбрать — его не видно.
                if ((building.Owner == playerSide) != own || building.IsDestroyed || !match.IsVisible(building, playerSide))
                {
                    continue;
                }

                float2 half = (float2)buildingViews.Style.SizeOf(building) * 0.5f + pad;
                float2 offset = math.abs(world - building.Position);
                float distance = math.distance(world, building.Position);
                bool inside = math.all(offset <= half) || distance <= config.BuildingTapRadius;
                if (inside && distance < bestDistance)
                {
                    best = building;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>
        /// Род войск группы своей толпы рекрутов под пальцем; null — толпы нет. У казармы фузилёры стоят левее середины
        /// толпы, гренадёры правее (если ждут только одни — тап по толпе выделяет их).
        /// </summary>
        private UnitType? FindOwnCampTypeAt(float2 world)
        {
            float minHalfExtent = cameraController.PixelsToWorld(ScreenUnits.DpToPixels(config.MinTouchTarget)) * 0.5f;
            foreach (Squad squad in match.Squads)
            {
                if (squad.Side != playerSide || !squad.IsCamp || squad.Alive <= 0
                    || !SquadFootprint.Of(squad, match.Settings.squads).Contains(world, minHalfExtent))
                {
                    continue;
                }

                if (squad.IsCavalryCamp)
                {
                    return UnitType.Cavalry;
                }

                var player = match.GetPlayer(playerSide);
                if (player.GetReserve(UnitType.Grenadiers) <= 0)
                {
                    return UnitType.Fusiliers;
                }

                if (player.GetReserve(UnitType.Fusiliers) <= 0)
                {
                    return UnitType.Grenadiers;
                }

                float lateral = math.dot(world - squad.Position, Angles.Right(squad.Facing));
                return lateral < 0f ? UnitType.Fusiliers : UnitType.Grenadiers;
            }

            return null;
        }

        private void SelectCamp(UnitType? type)
        {
            if (type == selectedCamp)
            {
                return;
            }

            if (type.HasValue)
            {
                Select(null);
                SelectBuilding(null);
            }

            selectedCamp = type;
            CampSelectionChanged?.Invoke(type);
        }

        private void SelectBuilding(Building building)
        {
            if (building == selectedBuilding)
            {
                return;
            }

            if (building != null)
            {
                Select(null);
                SelectCamp(null);
            }

            selectedBuilding = building;
            BuildingSelectionChanged?.Invoke(building);
        }

        private Squad FindSquadAt(float2 world, bool own)
        {
            float minHalfExtent = cameraController.PixelsToWorld(ScreenUnits.DpToPixels(config.MinTouchTarget)) * 0.5f;
            Squad best = null;
            float bestDistance = float.MaxValue;

            foreach (Squad squad in match.Squads)
            {
                // Бегущим приказов не отдать, а собираются они у казармы — тап должен доставаться ей.
                // Свою толпу рекрутов не выделить; вражескую можно выбрать целью атаки.
                if ((squad.Side == playerSide) != own || squad.IsRouting || (own && squad.IsCamp) || !match.IsVisible(squad, playerSide))
                {
                    continue;
                }

                if (!SquadFootprint.Of(squad, match.Settings.squads).Contains(world, minHalfExtent))
                {
                    continue;
                }

                float distance = math.distancesq(world, squad.Position);
                if (distance < bestDistance)
                {
                    best = squad;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>Выделить отряд снаружи — например, только что построенный из резерва.</summary>
        public void SelectSquad(Squad squad)
        {
            CancelPlacement();
            SelectBuilding(null);
            Select(squad);
        }

        private void Select(Squad squad)
        {
            if (squad == selected)
            {
                return;
            }

            if (squad != null)
            {
                SelectBuilding(null);
                SelectCamp(null);
            }

            selected = squad;
            group.Clear();
            if (squad != null)
            {
                group.Add(squad);
            }

            isRotating = false;
            rotationRing.SetSquad(squad);
            SetChargeTargeting(false);
            SelectionChanged?.Invoke(squad);
            GroupChanged?.Invoke(group);
        }

        /// <summary>Выделить набор своих отрядов (первый — главный); пустой — снять выделение.</summary>
        public void SetGroup(IList<Squad> squads)
        {
            var own = new List<Squad>();
            foreach (Squad squad in squads)
            {
                if (squad != null && squad.Side == playerSide && !squad.IsCamp && !squad.IsRouting && !own.Contains(squad)
                    && match.FindSquad(squad.Id) != null)
                {
                    own.Add(squad);
                }
            }

            if (own.Count == 0)
            {
                Select(null);
                return;
            }

            CancelPlacement();
            if (own[0] != selected)
            {
                Select(own[0]);
            }

            group.Clear();
            group.AddRange(own);
            GroupChanged?.Invoke(group);
        }

        /// <summary>Добавить отряд к выделению или убрать его оттуда (Shift/Ctrl + клик).</summary>
        public void ToggleInGroup(Squad squad)
        {
            var next = new List<Squad>(group);
            if (!next.Remove(squad))
            {
                next.Add(squad);
            }

            SetGroup(next);
        }

        /// <summary>Все свои отряды того же рода войск, что видны на экране.</summary>
        public void SelectSameTypeOnScreen(Squad sample)
        {
            var same = new List<Squad> { sample };
            foreach (Squad squad in match.Squads)
            {
                if (squad != sample && squad.Side == playerSide && !squad.IsCamp && !squad.IsRouting && squad.Type == sample.Type
                    && cameraController.IsVisible(squad.Position))
                {
                    same.Add(squad);
                }
            }

            SetGroup(same);
        }

        /// <summary>Свой отряд под точкой экрана (или null).</summary>
        public Squad FindOwnSquadAtScreen(Vector2 screen)
        {
            return FindSquadAt(cameraController.ScreenToWorld(screen), own: true);
        }

        public Squad FindEnemySquadAtScreen(Vector2 screen)
        {
            return FindSquadAt(cameraController.ScreenToWorld(screen), own: false);
        }

        public Building FindBuildingAtScreen(Vector2 screen, bool own)
        {
            return FindBuildingAt(cameraController.ScreenToWorld(screen), own);
        }

        /// <summary>
        /// Выделенные идут к точке: центр группы — в точку, взаимное расположение сохраняется. Фронт — заданный,
        /// а если не задан — от группы к точке.
        /// </summary>
        public void OrderMove(float2 destination, float? facing)
        {
            if (group.Count == 0)
            {
                return;
            }

            float2 center = GroupMove.Center(group);
            float2 toTarget = destination - center;
            float front = facing ?? (math.lengthsq(toTarget) > 1e-4f ? Angles.Of(toTarget) : selected.Facing);
            GroupMove.Enqueue(match, group, destination, front, selected.Facing, autoRoute: true);
        }

        /// <summary>Выделенные атакуют вражеский отряд: пехота и конница — в штыки, пушки — берут его целью.</summary>
        /// <param name="gunsOnly">Только пушкам указать цель (простой тап по врагу при выбранной пушке).</param>
        public void OrderAttack(Squad enemy, bool gunsOnly = false)
        {
            foreach (Squad squad in group)
            {
                if (gunsOnly && !squad.IsArtillery)
                {
                    continue;
                }

                if (squad.IsArtillery)
                {
                    if (squad.CanReceiveOrders)
                    {
                        match.Enqueue(new TargetCommand(squad.Id, enemy.Id));
                    }
                }
                else if (ChargeRules.CanCharge(squad))
                {
                    match.Enqueue(new ChargeCommand(squad.Id, enemy.Id));
                }
            }
        }

        /// <summary>Выделенные штурмуют вражеское здание (пушки — бьют по нему).</summary>
        public void OrderAssault(Building target, bool gunsOnly = false)
        {
            foreach (Squad squad in group)
            {
                if (gunsOnly && !squad.IsArtillery)
                {
                    continue;
                }

                if (squad.IsArtillery)
                {
                    if (squad.CanReceiveOrders)
                    {
                        match.Enqueue(TargetCommand.AtBuilding(squad.Id, target.Id));
                    }
                }
                else if (ChargeRules.CanCharge(squad))
                {
                    match.Enqueue(new AssaultCommand(squad.Id, target.Id));
                }
            }
        }

        /// <summary>Показать предпросмотр приказа главного отряда (протяжка правой кнопкой).</summary>
        public void ShowOrderPreview(float2 from, float2 to, float facing)
        {
            if (selected != null)
            {
                orderPreview.Show(from, to, facing, selected.TargetFormation, selected.Alive, match.Settings.squads);
            }
        }

        public void HideOrderPreview()
        {
            orderPreview.Hide();
        }

        /// <summary>Esc: отменить режим (постановка здания, выбор цели), иначе — снять выделение.</summary>
        public void CancelOrDeselect()
        {
            if (isPlacing)
            {
                CancelPlacement();
                return;
            }

            if (isChargeTargeting)
            {
                SetChargeTargeting(false);
                return;
            }

            Select(null);
            SelectBuilding(null);
            SelectCamp(null);
        }
    }
}
