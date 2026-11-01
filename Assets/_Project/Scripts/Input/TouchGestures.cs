using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Marchlands.Input
{
    /// <summary>
    /// Распознаёт жесты по касаниям EnhancedTouch: тап, протяжку одним пальцем и щипок двумя.
    /// Касание, начатое над UI, целиком отдаётся UI. После щипка жест не продолжается,
    /// пока не будут отпущены все пальцы — чтобы оставшийся палец не дал случайный тап или протяжку.
    /// </summary>
    public sealed class TouchGestures : IGestures
    {
        private enum State
        {
            Idle,
            Pressed,
            Dragging,
            Pinching,
            Blocked
        }

        private readonly InputConfig config;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        private State state;
        private int trackedTouchId;
        private Vector2 startPosition;
        private float previousPinchDistance;
        private Vector2 previousPinchCenter;

        public TouchGestures(InputConfig config)
        {
            this.config = config;
        }

        /// <summary>Тап: позиция на экране.</summary>
        public event Action<Vector2> Tapped;

        /// <summary>Начало протяжки: точка, где палец коснулся экрана.</summary>
        public event Action<Vector2> DragStarted;

        /// <summary>Протяжка продолжается: начальная и текущая точки.</summary>
        public event Action<Vector2, Vector2> DragUpdated;

        /// <summary>Палец отпущен: начальная и конечная точки.</summary>
        public event Action<Vector2, Vector2> DragEnded;

        /// <summary>Протяжка прервана (второй палец, отмена системой).</summary>
        public event Action DragCancelled;

        /// <summary>Щипок: центр между пальцами, его смещение за кадр и отношение нового расстояния к прежнему.</summary>
        public event Action<Vector2, Vector2, float> Pinched;

        public void Update()
        {
            var pressed = new PressedTouches();
            foreach (Touch touch in Touch.activeTouches)
            {
                if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
                {
                    pressed.Add(touch);
                }
            }

            if (pressed.Count >= 2)
            {
                UpdatePinch(pressed.First, pressed.Second);
                return;
            }

            if (state == State.Pinching || state == State.Blocked)
            {
                state = pressed.Count == 0 ? State.Idle : State.Blocked;
                return;
            }

            if (state == State.Idle)
            {
                TryBegin();
                return;
            }

            if (!TryFindTracked(out Touch tracked))
            {
                Cancel();
                return;
            }

            UpdateSingle(tracked);
        }

        private void TryBegin()
        {
            foreach (Touch touch in Touch.activeTouches)
            {
                if (touch.phase != TouchPhase.Began)
                {
                    continue;
                }

                trackedTouchId = touch.touchId;
                startPosition = touch.startScreenPosition;
                state = IsOverUi(startPosition, uiHits) ? State.Blocked : State.Pressed;
                return;
            }
        }

        private void UpdateSingle(Touch touch)
        {
            Vector2 position = touch.screenPosition;

            if (state == State.Pressed && Vector2.Distance(position, startPosition) > ScreenUnits.DpToPixels(config.TapMaxMove))
            {
                state = State.Dragging;
                DragStarted?.Invoke(startPosition);
            }

            switch (touch.phase)
            {
                case TouchPhase.Ended:
                    if (state == State.Dragging)
                    {
                        DragEnded?.Invoke(startPosition, position);
                    }
                    else if (touch.time - touch.startTime <= config.TapMaxDuration)
                    {
                        Tapped?.Invoke(position);
                    }

                    state = State.Idle;
                    break;

                case TouchPhase.Canceled:
                    Cancel();
                    break;

                default:
                    if (state == State.Dragging)
                    {
                        DragUpdated?.Invoke(startPosition, position);
                    }

                    break;
            }
        }

        private void UpdatePinch(Touch first, Touch second)
        {
            if (state == State.Dragging)
            {
                DragCancelled?.Invoke();
            }

            Vector2 center = (first.screenPosition + second.screenPosition) * 0.5f;
            float distance = Vector2.Distance(first.screenPosition, second.screenPosition);

            if (state != State.Pinching)
            {
                state = State.Pinching;
            }
            else if (previousPinchDistance > 0f && distance > 0f)
            {
                Pinched?.Invoke(center, center - previousPinchCenter, distance / previousPinchDistance);
            }

            previousPinchCenter = center;
            previousPinchDistance = distance;
        }

        private void Cancel()
        {
            if (state == State.Dragging)
            {
                DragCancelled?.Invoke();
            }

            state = State.Idle;
        }

        private bool TryFindTracked(out Touch tracked)
        {
            foreach (Touch touch in Touch.activeTouches)
            {
                if (touch.touchId == trackedTouchId)
                {
                    tracked = touch;
                    return true;
                }
            }

            tracked = default;
            return false;
        }

        internal static bool IsOverUi(Vector2 screenPosition, List<RaycastResult> uiHits)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            uiHits.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screenPosition }, uiHits);
            return uiHits.Count > 0;
        }

        /// <summary>Первые два прижатых пальца без выделения памяти.</summary>
        private struct PressedTouches
        {
            public int Count;
            public Touch First;
            public Touch Second;

            public void Add(Touch touch)
            {
                if (Count == 0)
                {
                    First = touch;
                }
                else if (Count == 1)
                {
                    Second = touch;
                }

                Count++;
            }
        }
    }
}
