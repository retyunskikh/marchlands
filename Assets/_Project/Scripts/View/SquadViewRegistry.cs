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
                Destroy(views[id].gameObject);
                views.Remove(id);
            }
        }
    }
}
