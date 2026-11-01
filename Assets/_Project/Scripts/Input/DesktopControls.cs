using System;
using System.Collections.Generic;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Marchlands.View;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Marchlands.Input
{
    /// <summary>
    /// Мышь и клавиатура на ПК поверх управления «пальцем» (левая кнопка — как палец, см. <see cref="MouseGestures"/>):
    /// правая кнопка — приказ (клик — идти или атаковать, протяжка — точка и фронт), Shift + протяжка — рамка
    /// выделения, Shift/Ctrl + клик — добавить или убрать отряд, Ctrl+1…9 — группы, WASD/стрелки, край экрана
    /// и средняя кнопка — карта, горячие клавиши приказов, подсветка под курсором. На телефоне выключено.
    /// </summary>
    public sealed class DesktopControls : MonoBehaviour
    {
        [SerializeField] private BattleInputController battleInput;
        [SerializeField] private SquadViewRegistry squadViews;
        [SerializeField] private BuildingViewRegistry buildingViews;
        [Tooltip("Спрайт рамки выделения (сплошной квадрат).")]
        [SerializeField] private Sprite boxSprite;
        [SerializeField] private Color boxColor = new Color(1f, 0.9f, 0.4f, 0.18f);
        [SerializeField] private int boxSortingOrder = 400;
        [Tooltip("Насколько сдвинуть мышь с Shift, чтобы началась рамка, а не клик, px.")]
        [SerializeField] private float boxMinDrag = 8f;
        [Tooltip("Двойное нажатие цифры группы за это время наводит на неё камеру, s.")]
        [SerializeField] private float groupDoublePressTime = 0.35f;
        [Tooltip("Двойной щелчок при постановке здания: за сколько секунд и в скольких пикселях — сразу «Поставить».")]
        [SerializeField] private float placeDoubleClickTime = 0.4f;
        [SerializeField] private float placeDoubleClickDistance = 24f;

        private float lastPlaceClickTime = -1f;
        private Vector2 lastPlaceClick;

        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private readonly List<int>[] groups = new List<int>[10];
        private readonly List<Squad> picked = new List<Squad>();

        private bool boxPressed;
        private bool boxDragging;
        private Vector2 boxStart;
        private SpriteRenderer box;

        private bool rightPressed;
        private bool rightDragging;
        private Vector2 rightStart;

        private bool middlePressed;
        private Vector2 middleLast;

        private int lastGroupKey = -1;
        private double lastGroupTime;

        private Squad hoveredSquad;
        private Building hoveredBuilding;
        private CursorMode cursorShown = CursorMode.Normal;
        private Texture2D targetCursor;

        private enum CursorMode
        {
            Normal,
            Target
        }

        /// <summary>Горячая клавиша залпа (у пушки — выбор цели).</summary>
        public event Action VolleyPressed;

        /// <summary>Горячая клавиша «В штыки».</summary>
        public event Action ChargePressed;

        /// <summary>Горячая клавиша «Марш» / «В линию».</summary>
        public event Action FormationPressed;

        public event Action StopPressed;

        public event Action PausePressed;

        /// <summary>Справка по клавишам (F1).</summary>
        public event Action HelpPressed;

        private void Update()
        {
            if (!BattleInputController.UsesMouse || battleInput == null || battleInput.Match == null)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 pointer = mouse.position.ReadValue();
            UpdateKeyboard(keyboard);
            UpdateCameraKeys(keyboard, pointer);
            UpdateMiddleDrag(mouse, pointer);
            UpdatePlacementDoubleClick(mouse, pointer);
            UpdateModifiedLeft(mouse, keyboard, pointer);
            UpdateRight(mouse, pointer);
            UpdateHover(pointer);
            UpdateCursor();
        }

        /// <summary>При постановке здания двойной щелчок по годному месту ставит его сразу, без «Поставить».</summary>
        private void UpdatePlacementDoubleClick(Mouse mouse, Vector2 pointer)
        {
            if (!battleInput.IsPlacing || !mouse.leftButton.wasPressedThisFrame || IsOverUi(pointer))
            {
                return;
            }

            float now = Time.unscaledTime;
            bool second = lastPlaceClickTime >= 0f && now - lastPlaceClickTime <= placeDoubleClickTime
                && Vector2.Distance(pointer, lastPlaceClick) <= placeDoubleClickDistance;
            lastPlaceClickTime = now;
            lastPlaceClick = pointer;
            if (second && battleInput.TryConfirmPlacement())
            {
                lastPlaceClickTime = -1f;
            }
        }

        private void OnDisable()
        {
            Cursor.SetCursor(null, Vector2.zero, UnityEngine.CursorMode.Auto);
        }

        private void UpdateKeyboard(Keyboard keyboard)
        {
            if (keyboard == null)
            {
                return;
            }

            InputConfig config = battleInput.Config;
            if (keyboard[config.CancelKey].wasPressedThisFrame)
            {
                battleInput.CancelOrDeselect();
            }

            if (keyboard[config.PauseKey].wasPressedThisFrame || keyboard[config.PauseAltKey].wasPressedThisFrame)
            {
                PausePressed?.Invoke();
            }

            if (keyboard[config.HelpKey].wasPressedThisFrame)
            {
                HelpPressed?.Invoke();
            }

            if (keyboard[config.HeadquartersKey].wasPressedThisFrame)
            {
                battleInput.ShowBuilding(FindOwnBuilding(BuildingType.Headquarters));
            }

            if (keyboard[config.BarracksKey].wasPressedThisFrame)
            {
                battleInput.ShowBuilding(FindOwnBuilding(BuildingType.Barracks));
            }

            if (keyboard[config.NextSquadKey].wasPressedThisFrame)
            {
                SelectNextSquad();
            }

            if (battleInput.Selected != null)
            {
                if (keyboard[config.VolleyKey].wasPressedThisFrame)
                {
                    VolleyPressed?.Invoke();
                }

                if (keyboard[config.ChargeKey].wasPressedThisFrame)
                {
                    ChargePressed?.Invoke();
                }

                if (keyboard[config.FormationKey].wasPressedThisFrame)
                {
                    FormationPressed?.Invoke();
                }

                if (keyboard[config.StopKey].wasPressedThisFrame)
                {
                    StopPressed?.Invoke();
                }
            }

            UpdateGroups(keyboard);
        }

        private Building FindOwnBuilding(BuildingType type)
        {
            foreach (Building building in battleInput.Match.Buildings)
            {
                if (building.Owner == battleInput.PlayerSide && building.Type == type && !building.IsDestroyed)
                {
                    return building;
                }
            }

            return null;
        }

        /// <summary>Tab — следующий свой отряд (по порядку появления), камера — на него.</summary>
        private void SelectNextSquad()
        {
            Squad current = battleInput.Selected;
            Squad first = null;
            Squad next = null;
            foreach (Squad squad in battleInput.Match.Squads)
            {
                if (squad.Side != battleInput.PlayerSide || squad.IsCamp || squad.IsRouting || squad.Alive <= 0)
                {
                    continue;
                }

                first ??= squad;
                if (current != null && squad.Id > current.Id && (next == null || squad.Id < next.Id))
                {
                    next = squad;
                }
            }

            Squad target = next ?? first;
            if (target == null)
            {
                return;
            }

            battleInput.SelectSquad(target);
            battleInput.CameraRig.CenterOn(new Vector2(target.Position.x, target.Position.y));
        }

        /// <summary>Ctrl+цифра — запомнить выделение; цифра — выбрать; цифра дважды — навести камеру.</summary>
        private void UpdateGroups(Keyboard keyboard)
        {
            for (int digit = 1; digit <= 9; digit++)
            {
                if (!keyboard[Key.Digit1 + (digit - 1)].wasPressedThisFrame)
                {
                    continue;
                }

                if (keyboard.ctrlKey.isPressed)
                {
                    groups[digit] ??= new List<int>();
                    groups[digit].Clear();
                    foreach (Squad squad in battleInput.SelectedGroup)
                    {
                        groups[digit].Add(squad.Id);
                    }

                    continue;
                }

                if (groups[digit] == null || groups[digit].Count == 0)
                {
                    continue;
                }

                picked.Clear();
                foreach (int id in groups[digit])
                {
                    Squad squad = battleInput.Match.FindSquad(id);
                    if (squad != null && !squad.IsRouting)
                    {
                        picked.Add(squad);
                    }
                }

                battleInput.SetGroup(picked);
                double now = Time.realtimeSinceStartupAsDouble;
                if (lastGroupKey == digit && now - lastGroupTime <= groupDoublePressTime && picked.Count > 0)
                {
                    battleInput.CameraRig.CenterOn(Simulation.Commands.GroupMove.Center(picked));
                }

                lastGroupKey = digit;
                lastGroupTime = now;
            }
        }

        private void UpdateCameraKeys(Keyboard keyboard, Vector2 pointer)
        {
            Vector2 direction = Vector2.zero;
            if (keyboard != null)
            {
                // Без Ctrl: Ctrl+цифры — группы, а WASD с Ctrl ни к чему.
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                {
                    direction.y += 1f;
                }

                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                {
                    direction.y -= 1f;
                }

                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    direction.x -= 1f;
                }

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    direction.x += 1f;
                }
            }

            // Курсор у края окна двигает карту (только когда окно в фокусе и курсор внутри него).
            float border = battleInput.Config.EdgePanBorder;
            if (border > 0f && Application.isFocused && pointer.x >= 0f && pointer.y >= 0f
                && pointer.x <= Screen.width && pointer.y <= Screen.height)
            {
                if (pointer.x <= border)
                {
                    direction.x -= 1f;
                }
                else if (pointer.x >= Screen.width - border)
                {
                    direction.x += 1f;
                }

                if (pointer.y <= border)
                {
                    direction.y -= 1f;
                }
                else if (pointer.y >= Screen.height - border)
                {
                    direction.y += 1f;
                }
            }

            if (direction == Vector2.zero)
            {
                return;
            }

            CameraController rig = battleInput.CameraRig;
            Vector2 step = Vector2.ClampMagnitude(direction, 1f) * battleInput.Config.KeyPanSpeed * rig.ViewHeight * Time.unscaledDeltaTime;
            rig.MoveBy(step);
        }

        private void UpdateMiddleDrag(Mouse mouse, Vector2 pointer)
        {
            if (mouse.middleButton.wasPressedThisFrame && !IsOverUi(pointer))
            {
                middlePressed = true;
                middleLast = pointer;
            }

            if (!mouse.middleButton.isPressed)
            {
                middlePressed = false;
                return;
            }

            if (middlePressed)
            {
                battleInput.CameraRig.Pan(pointer - middleLast);
                middleLast = pointer;
            }
        }

        /// <summary>Shift/Ctrl + левая: клик по своему отряду — добавить или убрать, протяжка — рамка выделения.</summary>
        private void UpdateModifiedLeft(Mouse mouse, Keyboard keyboard, Vector2 pointer)
        {
            if (mouse.leftButton.wasPressedThisFrame && MouseGestures.IsModifierHeld() && !IsOverUi(pointer) && !battleInput.IsPlacing)
            {
                boxPressed = true;
                boxDragging = false;
                boxStart = pointer;
            }

            if (!boxPressed)
            {
                return;
            }

            if (!boxDragging && Vector2.Distance(pointer, boxStart) > boxMinDrag)
            {
                boxDragging = true;
            }

            if (boxDragging)
            {
                ShowBox(boxStart, pointer);
            }

            if (mouse.leftButton.isPressed)
            {
                return;
            }

            boxPressed = false;
            HideBox();
            if (!boxDragging)
            {
                Squad hit = battleInput.FindOwnSquadAtScreen(pointer);
                if (hit != null)
                {
                    battleInput.ToggleInGroup(hit);
                }

                return;
            }

            // Рамка: свои отряды внутри; с Ctrl — добавить к выделенным, с Shift — выделить заново.
            bool add = keyboard != null && keyboard.ctrlKey.isPressed;
            picked.Clear();
            if (add)
            {
                picked.AddRange(battleInput.SelectedGroup);
            }

            Rect rect = ScreenRect(boxStart, pointer);
            foreach (Squad squad in battleInput.Match.Squads)
            {
                if (squad.Side == battleInput.PlayerSide && !squad.IsCamp && !squad.IsRouting && squad.Alive > 0
                    && rect.Contains(battleInput.CameraRig.WorldToScreen(squad.Position)) && !picked.Contains(squad))
                {
                    picked.Add(squad);
                }
            }

            battleInput.SetGroup(picked);
        }

        /// <summary>Правая кнопка: клик — идти или атаковать то, что под курсором; протяжка — точка и фронт.</summary>
        private void UpdateRight(Mouse mouse, Vector2 pointer)
        {
            if (mouse.rightButton.wasPressedThisFrame && !IsOverUi(pointer))
            {
                rightPressed = true;
                rightDragging = false;
                rightStart = pointer;
            }

            if (!rightPressed)
            {
                return;
            }

            if (battleInput.Selected == null)
            {
                rightPressed = false;
                battleInput.HideOrderPreview();
                return;
            }

            CameraController rig = battleInput.CameraRig;
            float2 from = rig.ScreenToWorld(rightStart);
            float2 to = rig.ScreenToWorld(pointer);
            if (!rightDragging && Vector2.Distance(pointer, rightStart) > ScreenUnits.DpToPixels(battleInput.Config.RightDragMinMove))
            {
                rightDragging = true;
            }

            if (rightDragging && mouse.rightButton.isPressed)
            {
                battleInput.ShowOrderPreview(from, from, Angles.Of(to - from));
                return;
            }

            if (mouse.rightButton.isPressed)
            {
                return;
            }

            rightPressed = false;
            battleInput.HideOrderPreview();
            if (rightDragging)
            {
                // Точка — где нажали, фронт — куда протянули.
                battleInput.OrderMove(from, Angles.Of(to - from));
                return;
            }

            Squad enemy = battleInput.FindEnemySquadAtScreen(pointer);
            if (enemy != null)
            {
                battleInput.OrderAttack(enemy);
                return;
            }

            Building enemyBuilding = battleInput.FindBuildingAtScreen(pointer, own: false);
            if (enemyBuilding != null)
            {
                battleInput.OrderAssault(enemyBuilding);
                return;
            }

            battleInput.OrderMove(to, null);
        }

        /// <summary>Подсветка отряда или здания под курсором.</summary>
        private void UpdateHover(Vector2 pointer)
        {
            Squad squad = null;
            Building building = null;
            if (!IsOverUi(pointer))
            {
                squad = battleInput.FindOwnSquadAtScreen(pointer) ?? battleInput.FindEnemySquadAtScreen(pointer);
                if (squad == null)
                {
                    building = battleInput.FindBuildingAtScreen(pointer, own: true) ?? battleInput.FindBuildingAtScreen(pointer, own: false);
                }
            }

            if (squad != hoveredSquad)
            {
                hoveredSquad = squad;
                squadViews.SetHovered(squad);
            }

            if (building != hoveredBuilding)
            {
                hoveredBuilding = building;
                buildingViews.SetHovered(building);
            }
        }

        /// <summary>В режиме выбора цели («В штыки», «Цель» пушки) курсор — прицел.</summary>
        private void UpdateCursor()
        {
            CursorMode wanted = battleInput.IsChargeTargeting ? CursorMode.Target : CursorMode.Normal;
            if (wanted == cursorShown)
            {
                return;
            }

            cursorShown = wanted;
            if (wanted == CursorMode.Target)
            {
                targetCursor ??= BuildTargetCursor();
                Cursor.SetCursor(targetCursor, new Vector2(targetCursor.width * 0.5f, targetCursor.height * 0.5f), UnityEngine.CursorMode.Auto);
            }
            else
            {
                Cursor.SetCursor(null, Vector2.zero, UnityEngine.CursorMode.Auto);
            }
        }

        /// <summary>Прицел: красный круг с перекрестием.</summary>
        private static Texture2D BuildTargetCursor()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var red = new Color(0.9f, 0.15f, 0.1f, 1f);
            var outline = new Color(0f, 0f, 0f, 0.85f);
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    bool ring = Mathf.Abs(distance - 11f) <= 1.2f;
                    bool cross = (Mathf.Abs(x - center) <= 0.8f || Mathf.Abs(y - center) <= 0.8f) && distance <= 14f && distance >= 4f;
                    bool shadow = Mathf.Abs(distance - 11f) <= 2.2f || ((Mathf.Abs(x - center) <= 1.8f || Mathf.Abs(y - center) <= 1.8f) && distance <= 15f && distance >= 3f);
                    texture.SetPixel(x, y, ring || cross ? red : shadow ? outline : Color.clear);
                }
            }

            texture.Apply();
            return texture;
        }

        private void ShowBox(Vector2 a, Vector2 b)
        {
            if (boxSprite == null)
            {
                return;
            }

            if (box == null)
            {
                var boxObject = new GameObject("Selection Box");
                box = boxObject.AddComponent<SpriteRenderer>();
                box.sprite = boxSprite;
                box.color = boxColor;
                box.sortingOrder = boxSortingOrder;
            }

            CameraController rig = battleInput.CameraRig;
            Vector2 worldA = rig.ScreenToWorld(a);
            Vector2 worldB = rig.ScreenToWorld(b);
            Vector2 size = new Vector2(Mathf.Abs(worldB.x - worldA.x), Mathf.Abs(worldB.y - worldA.y));
            Vector2 boundsSize = boxSprite.bounds.size;
            box.enabled = true;
            box.transform.position = new Vector3((worldA.x + worldB.x) * 0.5f, (worldA.y + worldB.y) * 0.5f, 0f);
            box.transform.localScale = new Vector3(size.x / boundsSize.x, size.y / boundsSize.y, 1f);
        }

        private void HideBox()
        {
            if (box != null)
            {
                box.enabled = false;
            }
        }

        private static Rect ScreenRect(Vector2 a, Vector2 b)
        {
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        private bool IsOverUi(Vector2 screen)
        {
            return TouchGestures.IsOverUi(screen, uiHits);
        }
    }
}
