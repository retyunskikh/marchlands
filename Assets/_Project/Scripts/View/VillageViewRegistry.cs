using System.Collections.Generic;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>Создаёт по одному <see cref="VillageView"/> на каждую деревню матча и отрисовывает их.</summary>
    public sealed class VillageViewRegistry : MonoBehaviour
    {
        [SerializeField] private VillageViewStyle style = new VillageViewStyle();
        [SerializeField] private SquadViewRegistry squadViews;

        private readonly List<VillageView> views = new List<VillageView>();

        public void Initialize(Match match)
        {
            foreach (Village village in match.Villages)
            {
                var viewObject = new GameObject($"Village {village.Id}");
                viewObject.transform.SetParent(transform, false);
                var view = viewObject.AddComponent<VillageView>();
                view.Initialize(village, match.Settings.economy.captureRadius, match.Settings.economy.captureTime, style, squadViews.Style);
                views.Add(view);
            }
        }

        public void Render()
        {
            foreach (VillageView view in views)
            {
                view.Render();
            }
        }
    }
}
