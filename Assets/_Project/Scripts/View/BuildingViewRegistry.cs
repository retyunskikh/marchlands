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
    /// и людей резерва, ждущих у казармы, где встанет новый отряд: новые выходят из дома казармы, распущенные уходят в него.
    /// </summary>
    public sealed class BuildingViewRegistry : MonoBehaviour
    {
        [SerializeField] private BuildingViewStyle style = new BuildingViewStyle();
        [SerializeField] private SquadViewRegistry squadViews;

        private readonly Dictionary<int, BuildingView> views = new Dictionary<int, BuildingView>();
        private readonly List<int> removed = new List<int>();
        private readonly List<Waiter>[] fusiliers = { new List<Waiter>(), new List<Waiter>() };
        private readonly List<Waiter>[] grenadiers = { new List<Waiter>(), new List<Waiter>() };
        private readonly List<Waiter>[] riders = { new List<Waiter>(), new List<Waiter>() };
        private readonly List<Waiter> leaving = new List<Waiter>();

        /// <summary>
        /// Человек у здания: где он сейчас нарисован, показан ли и какие точки пути ему осталось пройти.
        /// Новый выходит из двери дома казармы через ворота к своему месту в толпе; распущенный уходит обратно в дом.
        /// </summary>
        private sealed class Waiter
        {
            public readonly float2[] Path = new float2[3];
            public SpriteRenderer Renderer;
            public float2 Position;
            public bool Shown;
            public int PathLength;
            public int Step;

            public bool OnPath => Step < PathLength;

            public float2 NextPoint => Path[Step];
        }

        /// <summary>Дверь, из которой выходят к толпе, и, у казармы, ворота плаца: точка до и после них.</summary>
        private struct Route
        {
            public float2 Door;
            public bool HasGate;
            public float2 GateInside;
            public float2 GateOutside;
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

            // Разрушенные казармы и конюшни исчезают с карты (за кадр могло пройти несколько тиков).
            if (views.Count > match.Buildings.Count)
            {
                removed.Clear();
                foreach (KeyValuePair<int, BuildingView> pair in views)
                {
                    if (match.FindBuilding(pair.Key) == null)
                    {
                        removed.Add(pair.Key);
                    }
                }

                foreach (int id in removed)
                {
                    if (selected != null && selected.Id == id)
                    {
                        selected = null;
                    }

                    Destroy(views[id].gameObject);
                    views.Remove(id);
                }
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

            RenderLeaving();
        }

        /// <summary>Распущенные за тик люди уходят из толпы обратно в дом казармы или в конюшню.</summary>
        public void PlayDisbands(IReadOnlyList<Disband> disbands)
        {
            foreach (Disband disband in disbands)
            {
                List<Waiter> crowd = disband.Type == UnitType.Cavalry ? riders[(int)disband.Side]
                    : disband.Type == UnitType.Grenadiers ? grenadiers[(int)disband.Side] : fusiliers[(int)disband.Side];
                Building home = BuildRules.GetFormingBuilding(match, disband.Side, disband.Type);
                for (int i = crowd.Count - 1; i >= 0; i--)
                {
                    Waiter waiter = crowd[i];
                    if (!waiter.Shown)
                    {
                        continue;
                    }

                    crowd.RemoveAt(i);
                    if (home == null)
                    {
                        Destroy(waiter.Renderer.gameObject);
                        continue;
                    }

                    // Тот же путь, что от двери к толпе, только обратно: к воротам, сквозь них и в дверь.
                    Route route = GetRoute(home, waiter.Position, i);
                    waiter.PathLength = 0;
                    waiter.Step = 0;
                    if (route.HasGate)
                    {
                        waiter.Path[waiter.PathLength++] = route.GateOutside;
                        waiter.Path[waiter.PathLength++] = route.GateInside;
                    }

                    waiter.Path[waiter.PathLength++] = route.Door;
                    leaving.Add(waiter);
                }
            }
        }

        private void RenderLeaving()
        {
            float step = style.RecruitWalkSpeed * Time.deltaTime;
            for (int i = leaving.Count - 1; i >= 0; i--)
            {
                Waiter waiter = leaving[i];
                Walk(waiter, waiter.Position, step, waiter.Renderer.transform.rotation);
                if (!waiter.OnPath)
                {
                    Destroy(waiter.Renderer.gameObject);
                    leaving.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Шаг к цели: по точкам пути, если они остались, иначе к <paramref name="target"/>. Идущий смотрит,
        /// куда идёт; дошедший до места — в <paramref name="standing"/>.
        /// </summary>
        private static void Walk(Waiter waiter, float2 target, float step, Quaternion standing)
        {
            float2 goal = waiter.OnPath ? waiter.NextPoint : target;
            float2 toGoal = goal - waiter.Position;
            float distance = math.length(toGoal);
            Quaternion facing = standing;
            if (distance > step && distance > 1e-3f)
            {
                waiter.Position += toGoal / distance * step;
                facing = Quaternion.Euler(0f, 0f, math.degrees(math.atan2(toGoal.y, toGoal.x)) - 90f);
            }
            else
            {
                waiter.Position = goal;
                if (waiter.OnPath)
                {
                    waiter.Step++;
                    facing = waiter.Renderer.transform.rotation;
                }
            }

            waiter.Renderer.transform.SetPositionAndRotation(new Vector3(waiter.Position.x, waiter.Position.y, 0f), facing);
        }

        /// <summary>
        /// У казармы — одна из дверей её дома (выбор по <paramref name="pick"/>) и ворота плаца; у конюшни — край
        /// здания, обращённый к <paramref name="crowd"/>. Ворота смотрят к центру карты: здание справа отражено.
        /// </summary>
        private Route GetRoute(Building building, float2 crowd, int pick)
        {
            Vector2 size = building.Type == BuildingType.Stable ? style.StableSize : style.BarracksSize;
            Vector2[] doors = style.BarracksDoors;
            if (building.Type != BuildingType.Barracks || doors == null || doors.Length == 0)
            {
                float2 toCrowd = math.normalizesafe(crowd - building.Position, new float2(1f, 0f));
                return new Route { Door = building.Position + toCrowd * (math.min(size.x, size.y) * 0.5f) };
            }

            float direction = building.Position.x > 0f ? -1f : 1f;
            Vector2 door = doors[(int)((uint)(pick * 7 + building.Id * 3) % (uint)doors.Length)];
            Vector2 gate = style.BarracksGate;
            float2 gatePosition = building.Position + new float2(gate.x * size.x * direction, gate.y * size.y);
            float2 through = new float2(direction * style.GatePassDepth, 0f);
            return new Route
            {
                Door = building.Position + new float2(door.x * size.x * direction, door.y * size.y),
                HasGate = true,
                GateInside = gatePosition - through,
                GateOutside = gatePosition + through
            };
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
                    // Новый человек выходит из дома: через ворота плаца к своему месту.
                    Route route = GetRoute(barracks, start, i + match.CurrentTick);
                    waiter.Shown = true;
                    waiter.Position = route.Door;
                    waiter.Renderer.enabled = true;
                    waiter.PathLength = 0;
                    waiter.Step = 0;
                    if (route.HasGate)
                    {
                        waiter.Path[waiter.PathLength++] = route.GateInside;
                        waiter.Path[waiter.PathLength++] = route.GateOutside;
                    }
                }

                Walk(waiter, world, step, rotation);
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
