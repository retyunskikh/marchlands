using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Marchlands.Input
{
    /// <summary>
    /// Левая кнопка мыши как палец: клик — тап, протяжка — протяжка. Нажатие над UI целиком отдаётся UI.
    /// Нажатие с Shift или Ctrl жестом не считается — его разбирает <see cref="DesktopControls"/> (рамка, добавление
    /// к выделению). Щипка у мыши нет: масштаб — колесом.
    /// </summary>
    public sealed class MouseGestures : IGestures
    {
        private enum State
        {
            Idle,
            Pressed,
            Dragging,
            Blocked
        }

        private readonly InputConfig config;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private State state;
        private Vector2 startPosition;

        public MouseGestures(InputConfig config)
        {
            this.config = config;
        }

        public event Action<Vector2> Tapped;

        public event Action<Vector2> DragStarted;

        public event Action<Vector2, Vector2> DragUpdated;

        public event Action<Vector2, Vector2> DragEnded;

        public event Action DragCancelled;

        // Щипка у мыши нет: событие есть ради общего интерфейса.
        public event Action<Vector2, Vector2, float> Pinched
        {
            add { }
            remove { }
        }

        /// <summary>Нажатие с Shift или Ctrl — не жест, его разбирает управление мышью и клавиатурой.</summary>
        public static bool IsModifierHeld()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.shiftKey.isPressed || keyboard.ctrlKey.isPressed);
        }

        public void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 position = mouse.position.ReadValue();
            ButtonControl button = mouse.leftButton;
            if (button.wasPressedThisFrame)
            {
                startPosition = position;
                state = TouchGestures.IsOverUi(position, uiHits) || IsModifierHeld() ? State.Blocked : State.Pressed;
            }

            if (state == State.Idle || state == State.Blocked)
            {
                if (button.wasReleasedThisFrame || !button.isPressed)
                {
                    state = State.Idle;
                }

                return;
            }

            if (state == State.Pressed && Vector2.Distance(position, startPosition) > ScreenUnits.DpToPixels(config.TapMaxMove))
            {
                state = State.Dragging;
                DragStarted?.Invoke(startPosition);
            }

            if (button.wasReleasedThisFrame || !button.isPressed)
            {
                if (state == State.Dragging)
                {
                    DragEnded?.Invoke(startPosition, position);
                }
                else
                {
                    Tapped?.Invoke(position);
                }

                state = State.Idle;
                return;
            }

            if (state == State.Dragging)
            {
                DragUpdated?.Invoke(startPosition, position);
            }
        }

        /// <summary>Прервать протяжку (например, по Esc).</summary>
        public void Cancel()
        {
            if (state == State.Dragging)
            {
                DragCancelled?.Invoke();
            }

            state = State.Blocked;
        }
    }
}
