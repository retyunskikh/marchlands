using UnityEngine;

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

        public float TapMaxMove => tapMaxMove;

        public float TapMaxDuration => tapMaxDuration;

        public float DoubleTapMaxInterval => doubleTapMaxInterval;

        public float DoubleTapMaxDistance => doubleTapMaxDistance;

        public float MinTouchTarget => minTouchTarget;

        public float ZoomMin => zoomMin;

        public float ZoomMax => zoomMax;

        public float BuildingTapRadius => buildingTapRadius;

        public float WheelZoomFactor => wheelZoomFactor;
    }
}
