using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// Кольцо поворота вокруг выделенного отряда с ручкой-стрелкой по фронту. Пока ручку ведут по кольцу,
    /// она показывает новый фронт. Только отображение: жест разбирает ввод.
    /// </summary>
    public sealed class RotationRingView : MonoBehaviour
    {
        // Спрайт стрелки нарисован остриём вверх (+Y), а 0° в симуляции — это +X.
        private const float SpriteFacingOffset = -90f;

        [SerializeField] private Sprite ringSprite;
        [SerializeField] private Sprite handleSprite;
        [Tooltip("Насколько кольцо шире строя, u.")]
        [SerializeField] private float margin = 0.6f;
        [SerializeField] private float handleSize = 0.9f;
        [SerializeField] private Color ringColor = new Color(1f, 0.9f, 0.3f, 0.22f);
        [SerializeField] private Color handleColor = new Color(1f, 0.9f, 0.3f, 0.9f);
        [SerializeField] private Color activeColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private int sortingOrder = 52;

        private SpriteRenderer ring;
        private SpriteRenderer handle;
        private Match match;
        private Squad squad;
        private float? draggedFacing;

        public void Initialize(Match match)
        {
            this.match = match;
            ring = CreateRenderer("Ring", ringSprite, sortingOrder);
            handle = CreateRenderer("Handle", handleSprite, sortingOrder + 1);
            SetSquad(null);
        }

        /// <summary>Чьё кольцо показывать; null — скрыть.</summary>
        public void SetSquad(Squad squad)
        {
            this.squad = squad;
            draggedFacing = null;
        }

        /// <summary>Ручку ведут: показать новый фронт; null — ручка вернулась к фронту отряда.</summary>
        public void SetDraggedFacing(float? facing)
        {
            draggedFacing = facing;
        }

        /// <summary>Радиус кольца: чуть шире строя.</summary>
        public float GetRadius(Squad target)
        {
            float2 half = FormationLayout.GetHalfExtents(target.TargetFormation, target.Alive, match.Settings.squads, target.FileLimit);
            return math.max(half.x, half.y) + margin;
        }

        /// <summary>Попадает ли точка на кольцо (с допуском <paramref name="tolerance"/>, u).</summary>
        public bool IsOnRing(Squad target, float2 world, float tolerance)
        {
            float distance = math.distance(world, target.Position);
            float radius = GetRadius(target);
            return math.abs(distance - radius) <= tolerance + handleSize * 0.5f;
        }

        private void LateUpdate()
        {
            bool visible = squad != null && match != null && match.FindSquad(squad.Id) != null && squad.CanReceiveOrders;
            ring.enabled = visible;
            handle.enabled = visible;
            if (!visible)
            {
                return;
            }

            float radius = GetRadius(squad);
            float facing = draggedFacing ?? squad.Facing;
            Vector3 center = new Vector3(squad.Position.x, squad.Position.y, 0f);
            ring.transform.position = center;
            ring.transform.localScale = Vector3.one * (radius * 2f);
            ring.color = ringColor;

            float2 direction = Angles.Direction(facing);
            handle.transform.SetPositionAndRotation(center + new Vector3(direction.x, direction.y, 0f) * radius,
                Quaternion.Euler(0f, 0f, facing + SpriteFacingOffset));
            handle.transform.localScale = Vector3.one * handleSize;
            handle.color = draggedFacing.HasValue ? activeColor : handleColor;
        }

        private SpriteRenderer CreateRenderer(string objectName, Sprite sprite, int order)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var spriteRenderer = child.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = order;
            return spriteRenderer;
        }
    }
}
