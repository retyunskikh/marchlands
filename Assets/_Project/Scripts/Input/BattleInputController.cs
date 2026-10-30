using System;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Commands;
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
        private TouchGestures gestures;
        private Squad selected;

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

        private bool isChargeTargeting;

        private Building selectedBuilding;
        private bool isPlacing;
        private BuildingType placementType;
        private float2? placementSite;
        private bool lastPlacementConfirmable;

        /// <summary>Выделенный отряд изменился; null — выделение снято.</summary>
        public event Action<Squad> SelectionChanged;

        /// <summary>Выделенное здание изменилось; null — выделение снято.</summary>
        public event Action<Building> BuildingSelectionChanged;

        /// <summary>Режим постановки здания: включён ли он, выбрано ли место и можно ли там поставить здание.</summary>
        public event Action<bool, bool, bool> PlacementChanged;

        /// <summary>Включился или выключился выбор цели для «В штыки».</summary>
        public event Action<bool> ChargeTargetingChanged;

        public Squad Selected => selected;

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
        public void BeginChargeTargeting()
        {
            SetChargeTargeting(selected != null && ChargeRules.CanCharge(selected));
        }

        private void OnEnable()
        {
            // Создаём здесь, а не в Awake: после перекомпиляции в Play Mode Unity вызывает OnEnable без Awake.
            gestures ??= new TouchGestures(config);

            EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
            // В редакторе мышь изображает палец.
            TouchSimulation.Enable();
#endif
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
#if UNITY_EDITOR
            TouchSimulation.Disable();
#endif
            EnhancedTouchSupport.Disable();
        }

        private void Update()
        {
            if (match == null)
            {
                return;
            }

            // Выделенный отряд мог погибнуть.
            if (selected != null && match.FindSquad(selected.Id) == null)
            {
                hasPendingTap = false;
                Select(null);
            }

            // Выделенное здание могли разрушить.
            if (selectedBuilding != null && match.FindBuilding(selectedBuilding.Id) == null)
            {
                SelectBuilding(null);
            }

            if (isChargeTargeting && !ChargeRules.CanCharge(selected))
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
                Select(hit);
                return;
            }

            Building building = FindBuildingAt(world, own: true);
            if (building != null)
            {
                hasPendingTap = false;
                SelectBuilding(building);
                return;
            }

            if (selectedBuilding != null)
            {
                SelectBuilding(null);
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
                float2 toTarget = world - selected.Position;
                float facing = math.lengthsq(toTarget) > 0f ? Angles.Of(toTarget) : selected.Facing;
                match.Enqueue(new MoveCommand(selected.Id, world, facing));
                return;
            }

            hasPendingTap = true;
            pendingTapTime = now;
            pendingTapPosition = screenPosition;
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
                // Поворот на месте: движение в свою же точку с новым фронтом.
                match.Enqueue(new MoveCommand(selected.Id, selected.Position, FacingTowards(endScreen)));
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
                match.Enqueue(new MoveCommand(selected.Id, lockedDestination, lockedFacing));
                return;
            }

            float2 from = cameraController.ScreenToWorld(startScreen);
            float2 to = cameraController.ScreenToWorld(endScreen);
            match.Enqueue(new MoveCommand(selected.Id, to, Angles.Of(to - from)));
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
                match.Enqueue(new ChargeCommand(selected.Id, enemy.Id));
                return;
            }

            // Тап по вражескому зданию — штурм в штыки.
            Building enemyBuilding = FindBuildingAt(world, own: false);
            if (enemyBuilding != null)
            {
                match.Enqueue(new AssaultCommand(selected.Id, enemyBuilding.Id));
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
            bool confirmable = isPlacing && placementSite.HasValue
                && BuildRules.CanBuild(match, playerSide, placementType, placementSite.Value);
            if (isPlacing)
            {
                Building headquarters = match.GetHeadquarters(playerSide);
                Vector2? site = placementSite.HasValue ? (Vector2)placementSite.Value : (Vector2?)null;
                buildingViews.ShowPlacement(headquarters.Position, site, confirmable, placementType);
            }

            if (force || confirmable != lastPlacementConfirmable)
            {
                lastPlacementConfirmable = confirmable;
                PlacementChanged?.Invoke(isPlacing, placementSite.HasValue, confirmable);
            }
        }

        private Building FindBuildingAt(float2 world, bool own)
        {
            Building best = null;
            float bestDistance = config.BuildingTapRadius;
            foreach (Building building in match.Buildings)
            {
                float distance = math.distance(world, building.Position);
                if ((building.Owner == playerSide) == own && !building.IsDestroyed && distance <= bestDistance)
                {
                    best = building;
                    bestDistance = distance;
                }
            }

            return best;
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
                if ((squad.Side == playerSide) != own || squad.IsRouting || (own && squad.IsCamp))
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
            }

            selected = squad;
            isRotating = false;
            rotationRing.SetSquad(squad);
            SetChargeTargeting(false);
            SelectionChanged?.Invoke(squad);
        }
    }
}
