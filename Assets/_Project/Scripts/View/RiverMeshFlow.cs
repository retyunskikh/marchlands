using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// Течение реки на поле: блики на ленте вдоль русла плывут по течению — сдвигом текстуры вдоль ленты.
    /// Как <see cref="RiverFlow"/> у боковых рек, только для изогнутого русла. Только вид.
    /// </summary>
    public sealed class RiverMeshFlow : MonoBehaviour
    {
        private Mesh mesh;
        private Vector2[] baseUvs;
        private Vector2[] uvs;
        private float speed;

        /// <param name="speed">Скорость течения в долях текстуры в секунду.</param>
        internal void Initialize(Mesh target, float speed)
        {
            mesh = target;
            baseUvs = target.uv;
            uvs = new Vector2[baseUvs.Length];
            this.speed = speed;
        }

        private void Update()
        {
            if (mesh == null)
            {
                return;
            }

            float shift = Mathf.Repeat(Time.time * speed, 1f);
            for (int i = 0; i < baseUvs.Length; i++)
            {
                uvs[i] = new Vector2(baseUvs[i].x, baseUvs[i].y - shift);
            }

            mesh.uv = uvs;
        }
    }
}
