using System.Collections.Generic;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// Создаёт по одному <see cref="VillageView"/> на каждую деревню матча и отрисовывает их, а при пополнении отряда
    /// из деревни показывает солдата, который выходит из домика и идёт к отряду.
    /// </summary>
    public sealed class VillageViewRegistry : MonoBehaviour
    {
        [SerializeField] private VillageViewStyle style = new VillageViewStyle();
        [SerializeField] private SquadViewRegistry squadViews;
        [Tooltip("Скорость солдата, идущего из деревни к отряду, u/s.")]
        [SerializeField] private float walkerSpeed = 2.5f;
        [SerializeField] private int walkerSortingOrder = 21;

        private readonly List<VillageView> views = new List<VillageView>();
        private readonly List<Walker> walkers = new List<Walker>();

        private Match match;

        private sealed class Walker
        {
            public SpriteRenderer Renderer;
            public int SquadId;
            public float2 Position;
        }

        public void Initialize(Match match)
        {
            this.match = match;
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

            UpdateWalkers();
        }

        /// <summary>Каждое пополнение тика — солдат выходит из случайного домика деревни.</summary>
        public void Play(IReadOnlyList<Reinforcement> reinforcements)
        {
            foreach (Reinforcement reinforcement in reinforcements)
            {
                Squad squad = match.FindSquad(reinforcement.SquadId);
                Village village = FindVillage(reinforcement.VillageId);
                if (squad == null || village == null || style.HouseOffsets.Length == 0)
                {
                    continue;
                }

                Sprite sprite = squadViews.Style.GetSoldierSprite(squad.Side, squad.Type, SoldierRole.Private);
                var walkerObject = new GameObject($"Reinforcement {squad.Id}");
                walkerObject.transform.SetParent(transform, false);
                var spriteRenderer = walkerObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = sprite != null ? sprite : squadViews.Style.SoldierSprite;
                spriteRenderer.color = sprite != null ? Color.white : squadViews.Style.GetSideColor(squad.Side);
                spriteRenderer.sortingOrder = walkerSortingOrder;
                walkerObject.transform.localScale = Vector3.one * squadViews.Style.SoldierScale;
                int house = (int)((uint)(match.CurrentTick * 31 + squad.Id * 17) % (uint)style.HouseOffsets.Length);
                Vector2 offset = style.HouseOffsets[house];
                walkers.Add(new Walker
                {
                    Renderer = spriteRenderer,
                    SquadId = squad.Id,
                    Position = village.Position + new float2(offset.x, offset.y)
                });
            }
        }

        /// <summary>Солдат идёт к центру своего отряда и исчезает, дойдя до строя.</summary>
        private void UpdateWalkers()
        {
            for (int i = walkers.Count - 1; i >= 0; i--)
            {
                Walker walker = walkers[i];
                Squad squad = match.FindSquad(walker.SquadId);
                float2 toSquad = squad != null ? squad.Position - walker.Position : float2.zero;
                float distance = math.length(toSquad);
                float step = walkerSpeed * Time.deltaTime;
                if (squad == null || distance <= step + 0.3f)
                {
                    Destroy(walker.Renderer.gameObject);
                    walkers.RemoveAt(i);
                    continue;
                }

                float2 direction = toSquad / distance;
                walker.Position += direction * step;
                float angle = math.degrees(math.atan2(direction.y, direction.x)) - 90f;
                walker.Renderer.transform.SetPositionAndRotation(new Vector3(walker.Position.x, walker.Position.y, 0f), Quaternion.Euler(0f, 0f, angle));
            }
        }

        private Village FindVillage(int id)
        {
            foreach (Village village in match.Villages)
            {
                if (village.Id == id)
                {
                    return village;
                }
            }

            return null;
        }
    }
}
