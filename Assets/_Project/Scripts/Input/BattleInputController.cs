using System;
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
    /// Тач-управление боем (раздел 4 docs/design.md): выделение, приказы движения, камера.
    /// Состояние симуляции только читает, приказы отдаёт командами.
    /// </summary>
    public sealed class BattleInputController : MonoBehaviour
    {
        [SerializeField] private InputConfig config;
        [SerializeField] private CameraController cameraController;
        [SerializeField] private OrderPreviewView orderPreview;

        private Match match;
        private Side playerSide;
        private TouchGestures gestures;
        private Squad selected;

        private bool isDraggingOrder;
        private Vector2 lastDragPosition;

        private bool hasPendingTap;
        private double pendingTapTime;
        private Vector2 pendingTapPosition;

        private bool isChargeTargeting;

        /// <summary>Выделенный отряд изменился; null — выделение снято.</summary>
        public event Action<Squad> SelectionChanged;

        /// <summary>Включился или выключился выбор цели для «В штыки».</summary>
        public event Action<bool> ChargeTargetingChanged;

        public Squad Selected => selected;

        public void Initialize(Match match, Side playerSide)
        {
            this.match = match;
            this.playerSide = playerSide;
            cameraController.Initialize(config);
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

            if (isChargeTargeting && !ChargeRules.CanCharge(selected))
            {
                SetChargeTargeting(false);
            }

            gestures.Update();
            UpdateMouseWheel();

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

        private void OnDragStarted(Vector2 startScreen)
        {
            isDraggingOrder = selected != null && FindSquadAt(cameraController.ScreenToWorld(startScreen), own: true) == selected;
            lastDragPosition = startScreen;
            if (isDraggingOrder)
            {
                hasPendingTap = false;
            }
        }

        private void OnDragUpdated(Vector2 startScreen, Vector2 currentScreen)
        {
            if (!isDraggingOrder)
            {
                cameraController.Pan(currentScreen - lastDragPosition);
                lastDragPosition = currentScreen;
                return;
            }

            float2 from = cameraController.ScreenToWorld(startScreen);
            float2 to = cameraController.ScreenToWorld(currentScreen);
            orderPreview.Show(from, to, Angles.Of(to - from), selected.TargetFormation, selected.Alive, match.Settings.squads);
        }

        private void OnDragEnded(Vector2 startScreen, Vector2 endScreen)
        {
            if (!isDraggingOrder)
            {
                return;
            }

            isDraggingOrder = false;
            orderPreview.Hide();

            float2 from = cameraController.ScreenToWorld(startScreen);
            float2 to = cameraController.ScreenToWorld(endScreen);
            match.Enqueue(new MoveCommand(selected.Id, to, Angles.Of(to - from)));
        }

        private void OnDragCancelled()
        {
            isDraggingOrder = false;
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

        private Squad FindSquadAt(float2 world, bool own)
        {
            float minHalfExtent = cameraController.PixelsToWorld(ScreenUnits.DpToPixels(config.MinTouchTarget)) * 0.5f;
            Squad best = null;
            float bestDistance = float.MaxValue;

            foreach (Squad squad in match.Squads)
            {
                if ((squad.Side == playerSide) != own)
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

        private void Select(Squad squad)
        {
            if (squad == selected)
            {
                return;
            }

            selected = squad;
            SetChargeTargeting(false);
            SelectionChanged?.Invoke(squad);
        }
    }
}
