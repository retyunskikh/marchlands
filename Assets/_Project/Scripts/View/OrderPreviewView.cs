using System.Collections.Generic;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>Стрелка приказа и силуэт строя в конечной точке, пока игрок тянет палец.</summary>
    public sealed class OrderPreviewView : MonoBehaviour
    {
        // Спрайты стрелки и солдата нарисованы остриём/фронтом вверх (+Y), а 0° в симуляции — это +X.
        private const float SpriteFacingOffset = -90f;

        [SerializeField] private Sprite shaftSprite;
        [SerializeField] private Sprite headSprite;
        [SerializeField] private Sprite soldierSprite;
        [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.55f);
        [SerializeField] private float shaftWidth = 0.25f;
        [SerializeField] private float headSize = 1f;
        [SerializeField] private int sortingOrder = 30;

        private readonly List<SpriteRenderer> ghosts = new List<SpriteRenderer>();
        private readonly List<float2> slots = new List<float2>();

        private SpriteRenderer shaft;
        private SpriteRenderer head;

        private void Awake()
        {
            shaft = CreateRenderer("Shaft", shaftSprite);
            head = CreateRenderer("Head", headSprite);
            Hide();
        }

        /// <summary>Показывает стрелку от <paramref name="from"/> к <paramref name="to"/> и строй в точке <paramref name="to"/>.</summary>
        public void Show(float2 from, float2 to, float facing, Formation formation, int alive, SquadSettings settings)
        {
            float2 delta = to - from;
            float length = math.length(delta);
            float arrowAngle = length > 0f ? Angles.Of(delta) : facing;
            Quaternion arrowRotation = Quaternion.Euler(0f, 0f, arrowAngle + SpriteFacingOffset);

            // Остриё наконечника — ровно в точке приказа, древко доходит до основания наконечника.
            float2 direction = Angles.Direction(arrowAngle);
            float shaftLength = math.max(0f, length - headSize);
            shaft.enabled = shaftLength > 0f;
            shaft.transform.SetPositionAndRotation(ToVector3(from + direction * shaftLength * 0.5f), arrowRotation);
            shaft.transform.localScale = new Vector3(shaftWidth, shaftLength, 1f);

            head.enabled = true;
            head.transform.SetPositionAndRotation(ToVector3(to - direction * headSize * 0.5f), arrowRotation);
            head.transform.localScale = new Vector3(headSize, headSize, 1f);

            FormationLayout.GetLocalSlots(formation, alive, settings, slots);
            while (ghosts.Count < slots.Count)
            {
                ghosts.Add(CreateRenderer($"Ghost {ghosts.Count}", soldierSprite));
            }

            Quaternion soldierRotation = Quaternion.Euler(0f, 0f, facing + SpriteFacingOffset);
            for (int i = 0; i < ghosts.Count; i++)
            {
                bool used = i < slots.Count;
                ghosts[i].enabled = used;
                if (used)
                {
                    float2 world = FormationLayout.LocalToWorld(slots[i], to, facing);
                    ghosts[i].transform.SetPositionAndRotation(ToVector3(world), soldierRotation);
                }
            }
        }

        public void Hide()
        {
            shaft.enabled = false;
            head.enabled = false;
            foreach (SpriteRenderer ghost in ghosts)
            {
                ghost.enabled = false;
            }
        }

        private SpriteRenderer CreateRenderer(string objectName, Sprite sprite)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var spriteRenderer = child.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = sortingOrder;
            return spriteRenderer;
        }

        private static Vector3 ToVector3(float2 value)
        {
            return new Vector3(value.x, value.y, 0f);
        }
    }
}
