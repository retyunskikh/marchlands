using Unity.Mathematics;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// Шаг одиночной фигурки (рекрут, пополнение из деревни, рабочий): пока идёт — едва заметно покачивается
    /// с ноги на ногу и, если есть кадры шага, чередует «левой» и «правой»; остановилась — шаг плавно стихает.
    /// </summary>
    internal sealed class WalkerStep
    {
        private readonly float phase;
        private float blend;

        public WalkerStep(int seed)
        {
            uint hash = (uint)seed * 2654435761u;
            phase = (hash >> 8 & 0xFFFF) / 65535f * 2f * math.PI;
        }

        /// <summary>
        /// Ставит фигурку в <paramref name="position"/> с поворотом <paramref name="rotation"/> и шагом, если она идёт
        /// по направлению <paramref name="forward"/>. <paramref name="walk0"/>/<paramref name="walk1"/> — кадры шага
        /// (null — без кадров, только покачивание), <paramref name="stand"/> — фигурка стоя (null — не трогать спрайт).
        /// </summary>
        public void Apply(SpriteRenderer renderer, float2 position, Quaternion rotation, bool moving, float2 forward,
            Sprite stand, Sprite walk0, Sprite walk1, SquadViewStyle style)
        {
            float step = style.GaitBlendTime > 0f ? Time.deltaTime / style.GaitBlendTime : 1f;
            blend = moving ? math.min(1f, blend + step) : math.max(0f, blend - step);

            Vector2 gait = style.WalkerGait;
            float time = Time.time * gait.x * 2f * math.PI + phase;
            float2 shown = position;
            if (blend > 0f && math.lengthsq(forward) > 1e-6f)
            {
                float2 direction = math.normalize(forward);
                float2 side = new float2(direction.y, -direction.x);
                float amplitude = gait.y * math.smoothstep(0f, 1f, blend);
                shown += side * (math.sin(time) * amplitude) + direction * (math.abs(math.cos(time)) * amplitude * 0.5f);
            }

            renderer.transform.SetPositionAndRotation(new Vector3(shown.x, shown.y, 0f), rotation);

            if (stand == null)
            {
                return;
            }

            Sprite target = stand;
            if (blend > 0.3f)
            {
                Sprite walk = math.sin(time) >= 0f ? walk0 : walk1;
                if (walk != null)
                {
                    target = walk;
                }
            }

            if (renderer.sprite != target)
            {
                renderer.sprite = target;
            }
        }
    }
}
