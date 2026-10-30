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
        private readonly List<Waiter>[] fusiliers = { new List<Waiter>(), new List<Waiter>() };
        private readonly List<Waiter>[] grenadiers = { new List<Waiter>(), new List<Waiter>() };
        private readonly List<Waiter>[] riders = { new List<Waiter>(), new List<Waiter>() };

        /// <summary>Ждущий человек толпы: где он сейчас нарисован и показан ли (новый выходит из ворот).</summary>
        private sealed class Waiter
        {
            public SpriteRenderer Renderer;
            public float2 Position;
            public bool Shown;
        }

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
                // Пехота ждёт двумя группами в своём обмундировании: фузилёры слева от середины толпы, гренадёры справа.
                float groupOffset = math.max(1, style.RecruitsPerRow) * style.RecruitsSpacing.x * 0.5f + style.RecruitsGroupGap * 0.5f;
                RenderWaiting(side, UnitType.Fusiliers, player.FusilierReserve, fusiliers[(int)side], 1f, -groupOffset);
                RenderWaiting(side, UnitType.Grenadiers, player.GrenadierReserve, grenadiers[(int)side], 1f, groupOffset);
                RenderWaiting(side, UnitType.Cavalry, player.CavalryReserve, riders[(int)side], 1.8f, 0f);
            }
        }

        /// <summary>
        /// Ждущие люди резерва стоят толпой перед воротами здания, где формируется их род войск, лицом к фронту;
        /// рисуется не больше recruitsShownMax. Новый человек выходит из ворот здания и идёт на своё место.
        /// <paramref name="spread"/> — во сколько раз реже стоят (всадники крупнее); <paramref name="sideOffset"/> — сдвиг
        /// группы вправо от середины толпы, u.
        /// </summary>
        private void RenderWaiting(Side side, UnitType type, int waiting, List<Waiter> crowd, float spread, float sideOffset)
        {
            Sprite sprite = squadViews.Style.GetSoldierSprite(side, type, SoldierRole.Private);
            Building barracks = BuildRules.GetFormingBuilding(match, side, type);
            int shown = barracks == null || sprite == null
                ? 0
                : math.min(waiting, style.RecruitsShownMax);
            while (crowd.Count < shown)
            {
                SpriteRenderer person = CreatePreview($"Waiting {type} {side} {crowd.Count}", sprite, style.BodySortingOrder + 5);
                person.transform.localScale = Vector3.one * squadViews.Style.SoldierScale;
                crowd.Add(new Waiter { Renderer = person });
            }

            for (int i = shown; i < crowd.Count; i++)
            {
                crowd[i].Renderer.enabled = false;
                crowd[i].Shown = false;
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
            start += right * sideOffset;
            Quaternion rotation = Quaternion.Euler(0f, 0f, math.degrees(math.atan2(front.y, front.x)) - 90f);
            // Ворота — сторона здания, обращённая к толпе.
            Vector2 doorSize = barracks.Type == BuildingType.Stable ? style.StableSize : style.BarracksSize;
            float2 toCrowd = math.normalizesafe(start - barracks.Position, front);
            float2 door = barracks.Position + toCrowd * (math.min(doorSize.x, doorSize.y) * 0.5f);
            float step = style.RecruitWalkSpeed * Time.deltaTime;
            for (int i = 0; i < shown; i++)
            {
                int row = i / perRow;
                int column = i % perRow;
                // Толпа, а не строй: каждый чуть сбит со своего места.
                int seed = (int)side * 977 + (int)type * 131;
                float jitterX = Hash01(i * 2 + seed) - 0.5f;
                float jitterY = Hash01(i * 2 + 1 + seed) - 0.5f;
                float2 local = new float2(
                    (column - (perRow - 1) * 0.5f + jitterX * 0.6f) * style.RecruitsSpacing.x,
                    (row + jitterY * 0.5f) * style.RecruitsSpacing.y) * spread;
                float2 world = start + right * local.x + front * local.y;
                Waiter waiter = crowd[i];
                if (!waiter.Shown)
                {
                    waiter.Shown = true;
                    waiter.Position = door;
                    waiter.Renderer.enabled = true;
                }

                float2 toSlot = world - waiter.Position;
                float distance = math.length(toSlot);
                Quaternion facing = rotation;
                if (distance > step && distance > 1e-3f)
                {
                    waiter.Position += toSlot / distance * step;
                    facing = Quaternion.Euler(0f, 0f, math.degrees(math.atan2(toSlot.y, toSlot.x)) - 90f);
                }
                else
                {
                    waiter.Position = world;
                }

                waiter.Renderer.transform.SetPositionAndRotation(new Vector3(waiter.Position.x, waiter.Position.y, 0f), facing);
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
