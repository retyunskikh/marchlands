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
        private Squad selected;

        public SquadViewStyle Style => style;

        /// <summary>Размер здания на экране по типу (его задаёт реестр зданий) — чтобы штурмующие обступали стены.</summary>
        public System.Func<Marchlands.Simulation.Buildings.Building, Vector2> BuildingSize { get; set; }

        public void Initialize(Match match)
        {
            this.match = match;
        }

        public void SetSelected(Squad squad)
        {
            selected = squad;
            foreach (SquadView view in views.Values)
            {
                view.SetSelected(view.Squad == squad);
            }
        }

        /// <param name="alpha">Доля шага между предыдущим и текущим тиком.</param>
        public void Render(float alpha)
        {
            SyncViews();
            foreach (SquadView view in views.Values)
            {
                view.BuildingSize = BuildingSize;
                view.Render(alpha);
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
                view.SetSelected(squad == selected);
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
            }
        }
    }
}
