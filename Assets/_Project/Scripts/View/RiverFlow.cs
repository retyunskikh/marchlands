using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// Течение реки: слой бликов над водой плывёт вдоль русла и бесшовно повторяется через период текстуры.
    /// Только вид; на симуляцию не влияет.
    /// </summary>
    public sealed class RiverFlow : MonoBehaviour
    {
        private Vector3 origin;
        private float speed;
        private float period;

        internal void Initialize(float speed, float period)
        {
            origin = transform.localPosition;
            this.speed = speed;
            this.period = period;
        }

        private void Update()
        {
            if (period > 0f)
            {
                transform.localPosition = origin + new Vector3(0f, Mathf.Repeat(Time.time * speed, period), 0f);
            }
        }
    }
}
