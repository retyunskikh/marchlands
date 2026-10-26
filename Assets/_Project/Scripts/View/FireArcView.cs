using System.Collections.Generic;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// Сектор огня выделенного отряда тремя зонами: «в упор», «эффективно», «далеко». Форма совпадает
    /// с правилами симуляции: дистанция от линии фронта, границы — лучи от флангов под fireArcHalfAngle.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class FireArcView : MonoBehaviour
    {
        // Меш строится в локальных координатах фронта: x — вправо, y — вперёд; а 0° в симуляции — это +X.
        private const float MeshFacingOffset = -90f;

        [SerializeField] private Color pointBlankColor = new Color(0.9f, 0.25f, 0.2f, 0.28f);
        [SerializeField] private Color effectiveColor = new Color(1f, 0.75f, 0.25f, 0.22f);
        [SerializeField] private Color longColor = new Color(1f, 1f, 1f, 0.12f);
        [Tooltip("Сколько отрезков в дуге у каждого фланга.")]
        [SerializeField] private int arcSteps = 10;
        [SerializeField] private int sortingOrder = 5;

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();

        private Match match;
        private Squad squad;
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private float builtHalfWidth = -1f;

        public void Initialize(Match match)
        {
            this.match = match;
            mesh = new Mesh { name = "Fire Arc" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sortingOrder = sortingOrder;
            meshRenderer.enabled = false;
        }

        public void SetSquad(Squad squad)
        {
            this.squad = squad;
            builtHalfWidth = -1f;
        }

        public void Render(float alpha)
        {
            // Бегущий и конница не стреляют — сектор не нужен.
            bool visible = squad != null && match.FindSquad(squad.Id) != null
                && !squad.IsRouting && UnitStats.CanFire(squad, match.Settings);
            meshRenderer.enabled = visible;
            if (!visible)
            {
                return;
            }

            SquadSettings squads = match.Settings.squads;
            float2 half = FormationLayout.GetHalfExtents(squad.Formation, squad.Alive, squads);
            if (!Mathf.Approximately(half.x, builtHalfWidth))
            {
                BuildMesh(half.x, match.Settings.combat);
            }

            float2 position = math.lerp(squad.PreviousPosition, squad.Position, alpha);
            float facing = squad.PreviousFacing + Angles.Delta(squad.PreviousFacing, squad.Facing) * alpha;
            float2 front = position + Angles.Direction(facing) * half.y;
            transform.SetPositionAndRotation(new Vector3(front.x, front.y, 0f), Quaternion.Euler(0f, 0f, facing + MeshFacingOffset));
        }

        private void BuildMesh(float halfWidth, CombatSettings combat)
        {
            builtHalfWidth = halfWidth;
            vertices.Clear();
            colors.Clear();
            triangles.Clear();

            AddZone(halfWidth, 0f, combat.pointBlankRange, combat.fireArcHalfAngle, pointBlankColor);
            AddZone(halfWidth, combat.pointBlankRange, combat.effectiveRange, combat.fireArcHalfAngle, effectiveColor);
            AddZone(halfWidth, combat.effectiveRange, combat.maxRange, combat.fireArcHalfAngle, longColor);

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>Полоса между двумя «изолиниями» дистанции от фронта.</summary>
        private void AddZone(float halfWidth, float innerRadius, float outerRadius, float arcHalfAngle, Color color)
        {
            int first = vertices.Count;
            int pointsPerEdge = (arcSteps + 1) * 2;
            AddEdge(halfWidth, innerRadius, arcHalfAngle, color);
            AddEdge(halfWidth, outerRadius, arcHalfAngle, color);

            for (int i = 0; i < pointsPerEdge - 1; i++)
            {
                int inner = first + i;
                int outer = first + pointsPerEdge + i;
                triangles.Add(inner);
                triangles.Add(outer);
                triangles.Add(outer + 1);
                triangles.Add(inner);
                triangles.Add(outer + 1);
                triangles.Add(inner + 1);
            }
        }

        /// <summary>
        /// Точки на расстоянии <paramref name="radius"/> от фронта: дуга вокруг левого фланга от границы
        /// сектора до «прямо вперёд», затем такая же дуга вокруг правого фланга. Отрезок между дугами —
        /// линия, параллельная фронту.
        /// </summary>
        private void AddEdge(float halfWidth, float radius, float arcHalfAngle, Color color)
        {
            for (int i = 0; i <= arcSteps; i++)
            {
                float angle = math.radians(90f + arcHalfAngle * (1f - (float)i / arcSteps));
                vertices.Add(new Vector3(-halfWidth + radius * math.cos(angle), radius * math.sin(angle), 0f));
                colors.Add(color);
            }

            for (int i = 0; i <= arcSteps; i++)
            {
                float angle = math.radians(90f - arcHalfAngle * i / arcSteps);
                vertices.Add(new Vector3(halfWidth + radius * math.cos(angle), radius * math.sin(angle), 0f));
                colors.Add(color);
            }
        }
    }
}
