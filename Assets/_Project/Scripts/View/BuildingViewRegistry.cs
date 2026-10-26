using System.Collections.Generic;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// Держит по одному <see cref="BuildingView"/> на каждое здание матча; рисует предпросмотр постройки
    /// и людей резерва, ждущих у казармы, где встанет новый отряд.
    /// </summary>
    public sealed class BuildingViewRegistry : MonoBehaviour
    {
        [SerializeField] private BuildingViewStyle style = new BuildingViewStyle();
        [SerializeField] private SquadViewRegistry squadViews;

        private readonly Dictionary<int, BuildingView> views = new Dictionary<int, BuildingView>();
        private readonly List<SpriteRenderer>[] recruits = { new List<SpriteRenderer>(), new List<SpriteRenderer>() };
        private readonly List<SpriteRenderer>[] riders = { new List<SpriteRenderer>(), new List<SpriteRenderer>() };

        private Match match;
        private Building selected;
        private SpriteRenderer radiusPreview;
        private SpriteRenderer sitePreview;

        public BuildingViewStyle Style => style;

        public void Initialize(Match match)
        {
            this.match = match;
            radiusPreview = CreatePreview("Build Radius", style.CircleSprite, style.BodySortingOrder - 2);
            sitePreview = CreatePreview("Barracks Site", style.SquareSprite, style.OverlaySortingOrder);
            sitePreview.transform.localScale = new Vector3(style.BarracksSize.x, style.BarracksSize.y, 1f);
            HidePlacement();
        }

        public void SetSelected(Building building)
        {
            selected = building;
            foreach (BuildingView view in views.Values)
            {
                view.SetSelected(view.Building == building);
            }
        }

        public void Render()
        {
            foreach (Building building in match.Buildings)
            {
                if (views.ContainsKey(building.Id))
                {
                    continue;
                }

                var viewObject = new GameObject($"{building.Type} {building.Id} ({building.Owner})");
                viewObject.transform.SetParent(transform, false);
                var view = viewObject.AddComponent<BuildingView>();
                view.Initialize(building, match.Settings, style, squadViews.Style.GetSideColor(building.Owner));
                view.SetSelected(building == selected);
                views.Add(building.Id, view);
            }

            foreach (BuildingView view in views.Values)
            {
                view.Render(match);
            }

            foreach (Side side in new[] { Side.France, Side.Austria })
            {
                PlayerState player = match.GetPlayer(side);
                RenderWaiting(side, UnitType.Grenadiers, player.GrenadierReserve + player.FusilierReserve, recruits[(int)side], squadViews.Style.RecruitSprite, 1f);
                RenderWaiting(side, UnitType.Cavalry, player.CavalryReserve, riders[(int)side],
                    squadViews.Style.GetSoldierSprite(side, UnitType.Cavalry, SoldierRole.Private), 1.8f);
            }
        }

        /// <summary>
        /// Ждущие люди резерва стоят толпой перед воротами здания, где формируется их род войск, лицом к фронту;
        /// рисуется не больше recruitsShownMax. <paramref name="spread"/> — во сколько раз реже стоят (всадники крупнее).
        /// </summary>
        private void RenderWaiting(Side side, UnitType type, int waiting, List<SpriteRenderer> crowd, Sprite sprite, float spread)
        {
            Building barracks = BuildRules.GetFormingBuilding(match, side, type);
            int shown = barracks == null || sprite == null
                ? 0
                : math.min(waiting, style.RecruitsShownMax);
            while (crowd.Count < shown)
            {
                SpriteRenderer person = CreatePreview($"Waiting {type} {side} {crowd.Count}", sprite, style.BodySortingOrder + 5);
                person.transform.localScale = Vector3.one * squadViews.Style.SoldierScale;
                crowd.Add(person);
            }

            for (int i = 0; i < crowd.Count; i++)
            {
                crowd[i].enabled = i < shown;
            }

            if (shown == 0)
            {
                return;
            }

            // Толпа стоит там же, где её «отряд» в симуляции (по нему можно стрелять); без него — перед воротами.
            int perRow = math.max(1, style.RecruitsPerRow);
            Squad camp = FindCamp(side, type == UnitType.Cavalry);
            float2 front;
            float2 start;
            if (camp != null)
            {
                front = Angles.Direction(camp.Facing);
                int rows = (shown + perRow - 1) / perRow;
                start = camp.Position - front * ((rows - 1) * 0.5f * style.RecruitsSpacing.y * spread);
            }
            else
            {
                front = new float2(barracks.Position.x > 0f ? -1f : 1f, 0f);
                Vector2 buildingSize = barracks.Type == BuildingType.Stable ? style.StableSize : style.BarracksSize;
                start = barracks.Position + front * (buildingSize.x * 0.5f + style.RecruitsOffset);
            }

            float2 right = new float2(front.y, -front.x);
            Quaternion rotation = Quaternion.Euler(0f, 0f, math.degrees(math.atan2(front.y, front.x)) - 90f);
            for (int i = 0; i < shown; i++)
            {
                int row = i / perRow;
                int column = i % perRow;
                // Толпа, а не строй: каждый чуть сбит со своего места.
                float jitterX = Hash01(i * 2 + (int)side * 977) - 0.5f;
                float jitterY = Hash01(i * 2 + 1 + (int)side * 977) - 0.5f;
                float2 local = new float2(
                    (column - (perRow - 1) * 0.5f + jitterX * 0.6f) * style.RecruitsSpacing.x,
                    (row + jitterY * 0.5f) * style.RecruitsSpacing.y) * spread;
                float2 world = start + right * local.x + front * local.y;
                crowd[i].transform.SetPositionAndRotation(new Vector3(world.x, world.y, 0f), rotation);
            }
        }

        private Squad FindCamp(Side side, bool cavalry)
        {
            foreach (Squad squad in match.Squads)
            {
                if (squad.IsCamp && squad.Side == side && squad.IsCavalryCamp == cavalry && squad.Alive > 0)
                {
                    return squad;
                }
            }

            return null;
        }

        private static float Hash01(int value)
        {
            uint hash = (uint)value * 2654435761u;
            hash ^= hash >> 16;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            return (hash & 0xFFFFFF) / (float)0x1000000;
        }

        /// <summary>Круг допустимого радиуса вокруг штаба и, если место выбрано, силуэт здания зелёным или красным.</summary>
        public void ShowPlacement(Vector2 headquarters, Vector2? site, bool valid, BuildingType type)
        {
            Vector2 size = type == BuildingType.Stable ? style.StableSize : style.BarracksSize;
            sitePreview.transform.localScale = new Vector3(size.x, size.y, 1f);
            radiusPreview.enabled = true;
            radiusPreview.transform.position = headquarters;
            radiusPreview.transform.localScale = Vector3.one * (match.Settings.buildings.buildRadiusFromHQ * 2f);
            radiusPreview.color = style.BuildRadiusColor;

            sitePreview.enabled = site.HasValue;
            if (site.HasValue)
            {
                sitePreview.transform.position = site.Value;
                sitePreview.color = valid ? style.ValidSiteColor : style.InvalidSiteColor;
            }
        }

        public void HidePlacement()
        {
            radiusPreview.enabled = false;
            sitePreview.enabled = false;
        }

        private SpriteRenderer CreatePreview(string objectName, Sprite sprite, int sortingOrder)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var spriteRenderer = child.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = sortingOrder;
            return spriteRenderer;
        }
    }
}
