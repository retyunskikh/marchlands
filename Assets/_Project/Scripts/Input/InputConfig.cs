using UnityEngine;
using UnityEngine.InputSystem;

namespace Marchlands.Input
{
    /// <summary>Пороги жестов и пределы камеры (раздел 6 docs/design.md).</summary>
    [CreateAssetMenu(menuName = "Marchlands/Input Config", fileName = "Input")]
    public sealed class InputConfig : ScriptableObject
    {
        [Tooltip("Насколько может сместиться палец, чтобы касание осталось тапом, dp.")]
        [SerializeField] private float tapMaxMove;
        [Tooltip("Максимальная длительность тапа, s.")]
        [SerializeField] private float tapMaxDuration;
        [Tooltip("Максимальный интервал между тапами двойного тапа, s.")]
        [SerializeField] private float doubleTapMaxInterval;
        [Tooltip("Максимальное расстояние между тапами двойного тапа, dp.")]
        [SerializeField] private float doubleTapMaxDistance;
        [Tooltip("Минимальная зона касания отряда, dp.")]
        [SerializeField] private float minTouchTarget;
        [Tooltip("Минимальный ортографический размер камеры.")]
        [SerializeField] private float zoomMin;
        [Tooltip("Максимальный ортографический размер камеры.")]
        [SerializeField] private float zoomMax;
        [Tooltip("Радиус попадания тапом по зданию, u.")]
        [SerializeField] private float buildingTapRadius;
        [Tooltip("Множитель масштаба за одно деление колеса мыши (для редактора).")]
        [SerializeField] private float wheelZoomFactor;
        [Tooltip("Допуск попадания пальцем по кольцу поворота, dp.")]
        [SerializeField] private float rotationRingGrab;
        [Tooltip("Сколько держать палец неподвижно при протяжке приказа, чтобы закрепить точку и задавать фронт, s.")]
        [SerializeField] private float holdToAimTime = 0.45f;
        [Tooltip("Насколько палец может сдвинуться и всё ещё считаться неподвижным, dp.")]
        [SerializeField] private float holdStillTolerance = 10f;

        [Header("Мышь и клавиатура (ПК)")]
        [Tooltip("Скорость прокрутки карты клавишами и у края экрана — доля видимой высоты в секунду.")]
        [SerializeField] private float keyPanSpeed = 0.9f;
        [Tooltip("Ширина полосы у края экрана, где курсор двигает карту, px (0 — выключено).")]
        [SerializeField] private float edgePanBorder = 12f;
        [Tooltip("Насколько сдвинуть правую кнопку, чтобы это была протяжка (фронт), а не клик, dp.")]
        [SerializeField] private float rightDragMinMove = 12f;
        [SerializeField] private Key volleyKey = Key.F;
        [SerializeField] private Key chargeKey = Key.C;
        [SerializeField] private Key formationKey = Key.R;
        [SerializeField] private Key stopKey = Key.X;
        [SerializeField] private Key pauseKey = Key.Space;
        [SerializeField] private Key cancelKey = Key.Escape;

        public float KeyPanSpeed => keyPanSpeed;

        public float EdgePanBorder => edgePanBorder;

        public float RightDragMinMove => rightDragMinMove;

        public Key VolleyKey => volleyKey;

        public Key ChargeKey => chargeKey;

        public Key FormationKey => formationKey;

        public Key StopKey => stopKey;

        public Key PauseKey => pauseKey;

        public Key CancelKey => cancelKey;

        public float TapMaxMove => tapMaxMove;

        public float TapMaxDuration => tapMaxDuration;

        public float DoubleTapMaxInterval => doubleTapMaxInterval;

        public float DoubleTapMaxDistance => doubleTapMaxDistance;

        public float MinTouchTarget => minTouchTarget;

        public float ZoomMin => zoomMin;

        public float ZoomMax => zoomMax;

        public float BuildingTapRadius => buildingTapRadius;

        public float WheelZoomFactor => wheelZoomFactor;

        public float RotationRingGrab => rotationRingGrab;

        public float HoldToAimTime => holdToAimTime;

        public float HoldStillTolerance => holdStillTolerance;
    }
}
