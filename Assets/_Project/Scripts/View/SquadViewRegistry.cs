using System.Collections.Generic;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>Держит по одному <see cref="SquadView"/> на каждый отряд матча и отрисовывает их.</summary>
    public sealed class SquadViewRegistry : MonoBehaviour
    {
        [SerializeField] private SquadViewStyle style = new SquadViewStyle();
        [Tooltip("Куда уходят фигурки павших: вспышка ранения, падение, исчезновение.")]
        [SerializeField] private CasualtiesView casualties;

        private readonly Dictionary<int, SquadView> views = new Dictionary<int, SquadView>();
        private readonly HashSet<int> aliveIds = new HashSet<int>();
        private readonly List<int> staleIds = new List<int>();

        private Match match;
        private Side viewer;
        private readonly HashSet<int> selectedIds = new HashSet<int>();

        [Tooltip("За сколько секунд отряд, ушедший в туман войны (или вышедший из него), гаснет (проявляется).")]
        [SerializeField] private float fogFadeTime = 0.5f;

        // Туман войны: насколько виден отряд (1 — полностью, 0 — скрыт) и исходная прозрачность его спрайтов.
        private readonly Dictionary<int, float> fogAlpha = new Dictionary<int, float>();
        private readonly Dictionary<SpriteRenderer, float> baseAlpha = new Dictionary<SpriteRenderer, float>();
        private readonly List<SpriteRenderer> fadeRenderers = new List<SpriteRenderer>();
        private int hoveredId = -1;

        public SquadViewStyle Style => style;

        /// <summary>Размер здания на экране по типу (его задаёт реестр зданий) — чтобы штурмующие обступали стены.</summary>
        public System.Func<Marchlands.Simulation.Buildings.Building, Vector2> BuildingSize { get; set; }

        /// <param name="viewer">Чьими глазами смотрим: чужие отряды в тумане войны скрыты.</param>
        public void Initialize(Match match, Side viewer = Side.France)
        {
            this.match = match;
            this.viewer = viewer;
        }

        /// <summary>Виден ли отряд игроку (туман войны). Для людей, идущих к отряду, и т. п.</summary>
        public bool IsShown(Squad squad)
        {
            return match.IsVisible(squad, viewer);
        }

        public void SetSelected(Squad squad)
        {
            SetSelected(squad != null ? new[] { squad } : System.Array.Empty<Squad>());
        }

        /// <summary>Выделенные отряды (кольцо у каждого).</summary>
        public void SetSelected(IReadOnlyList<Squad> squads)
        {
            selectedIds.Clear();
            foreach (Squad squad in squads)
            {
                selectedIds.Add(squad.Id);
            }

            foreach (SquadView view in views.Values)
            {
                view.SetSelected(selectedIds.Contains(view.Squad.Id));
            }
        }

        /// <summary>Отряд под курсором мыши; null — никого.</summary>
        public void SetHovered(Squad squad)
        {
            int id = squad != null ? squad.Id : -1;
            if (id == hoveredId)
            {
                return;
            }

            if (views.TryGetValue(hoveredId, out SquadView previous))
            {
                previous.SetHovered(false);
            }

            hoveredId = id;
            if (views.TryGetValue(hoveredId, out SquadView current))
            {
                current.SetHovered(true);
            }
        }

        /// <param name="alpha">Доля шага между предыдущим и текущим тиком.</param>
        public void Render(float alpha)
        {
            SyncViews();
            foreach (SquadView view in views.Values)
            {
                // Туман войны: чужой отряд вне обзора плавно гаснет, совсем скрытый не рисуется.
                float shown = UpdateFog(view);
                if (shown <= 0f)
                {
                    continue;
                }

                view.BuildingSize = BuildingSize;
                view.Render(alpha);
                ApplyFog(view, shown);
            }
        }

        private void SyncViews()
        {
            aliveIds.Clear();
            foreach (Squad squad in match.Squads)
            {
                // Толпу рекрутов рисует реестр зданий.
                if (squad.IsCamp)
                {
                    continue;
                }

                aliveIds.Add(squad.Id);
                if (views.ContainsKey(squad.Id))
                {
                    continue;
                }

                var viewObject = new GameObject($"Squad {squad.Id} ({squad.Side})");
                viewObject.transform.SetParent(transform, false);
                var view = viewObject.AddComponent<SquadView>();
                view.Initialize(squad, match, style, casualties);
                view.BuildingSize = BuildingSize;
                view.SetSelected(selectedIds.Contains(squad.Id));
                view.SetHovered(squad.Id == hoveredId);
                views.Add(squad.Id, view);
            }

            staleIds.Clear();
            foreach (int id in views.Keys)
            {
                if (!aliveIds.Contains(id))
                {
                    staleIds.Add(id);
                }
            }

            foreach (int id in staleIds)
            {
                // Отряд погиб целиком (например, одним залпом): его солдаты падают и тают, как все павшие,
                // а не пропадают вместе с отрядом.
                if (views[id].Squad.Alive <= 0)
                {
                    views[id].ReleaseFallen();
                }

                Destroy(views[id].gameObject);
                views.Remove(id);
                fogAlpha.Remove(id);
            }
        }

        private float UpdateFog(SquadView view)
        {
            float target = match.IsVisible(view.Squad, viewer) ? 1f : 0f;
            if (!fogAlpha.TryGetValue(view.Squad.Id, out float shown))
            {
                // Новый отряд сразу в своём состоянии, без проявления.
                shown = target;
            }

            float step = fogFadeTime > 0f ? Time.deltaTime / fogFadeTime : 1f;
            shown = Mathf.MoveTowards(shown, target, step);
            fogAlpha[view.Squad.Id] = shown;
            bool active = shown > 0f;
            if (view.gameObject.activeSelf != active)
            {
                view.gameObject.SetActive(active);
            }

            return shown;
        }

        /// <summary>Прозрачность всех спрайтов отряда умножается на <paramref name="shown"/>; при полной видимости — как было.</summary>
        private void ApplyFog(SquadView view, float shown)
        {
            if (shown >= 1f && !fogAlpha.ContainsKey(-view.Squad.Id))
            {
                return;
            }

            view.GetComponentsInChildren(true, fadeRenderers);
            foreach (SpriteRenderer spriteRenderer in fadeRenderers)
            {
                if (!baseAlpha.TryGetValue(spriteRenderer, out float original))
                {
                    original = spriteRenderer.color.a;
                    baseAlpha[spriteRenderer] = original;
                }

                Color color = spriteRenderer.color;
                color.a = original * shown;
                spriteRenderer.color = color;
            }

            // Пока отряд полупрозрачен, помним это (ключ с минусом), чтобы на полной видимости вернуть прозрачность.
            if (shown >= 1f)
            {
                fogAlpha.Remove(-view.Squad.Id);
                foreach (SpriteRenderer spriteRenderer in fadeRenderers)
                {
                    baseAlpha.Remove(spriteRenderer);
                }
            }
            else
            {
                fogAlpha[-view.Squad.Id] = shown;
            }
        }
    }
}
