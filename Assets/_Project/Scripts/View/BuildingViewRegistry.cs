using System.Collections.Generic;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Combat;
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
        private readonly Dictionary<int, Sentries> sentries = new Dictionary<int, Sentries>();
        private readonly Dictionary<int, List<Worker>> crews = new Dictionary<int, List<Worker>>();
        private readonly HashSet<int> assaulted = new HashSet<int>();
        private readonly List<int> finishedCrews = new List<int>();

        // Фигурки толпы по сторонам (пехота: сначала фузилёры, потом гренадёры; конница — всадники):
        // по ним выстрел вразнобой выбирает, кто из толпы целится и стреляет.
        private readonly List<Waiter>[] infantryCampFigures = { new List<Waiter>(), new List<Waiter>() };
        private readonly List<Waiter>[] cavalryCampFigures = { new List<Waiter>(), new List<Waiter>() };
        private readonly HashSet<Waiter> aimingWaiters = new HashSet<Waiter>();

        /// <summary>Часовые на углах ограды здания; после залпа какое-то время смотрят на цель.</summary>
        private sealed class Sentries
        {
            public SpriteRenderer[] Figures;
            public SpriteRenderer[] Arms;
            public Sprite Stand;
            public Sprite Body;
            public float AlertUntil;
            public float2 Target;
            public float AimBlend;
        }

        /// <summary>Рабочий из штаба: идёт к повреждённому зданию, чинит его и возвращается в штаб.</summary>
        private sealed class Worker
        {
            public SpriteRenderer Renderer;
            public float2 Position;
            public float2 Post;
            public bool Returning;
            public WalkerStep Step;
        }

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
            public WalkerStep Stride;
            public Sprite Stand;
            public Sprite Walk0;
            public Sprite Walk1;

            // «Целься!» в толпе: тело без ружья и руки с ружьём, вынесенные вперёд (null — без позы).
            public Sprite Body;
            public SpriteRenderer Arms;
            public float AimBlend;
            public float2 AimTarget;

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
        private Side selectedCampSide;
        private UnitType? selectedCampType;
        private SpriteRenderer campMarker;
        private SpriteRenderer radiusPreview;
        private SpriteRenderer sitePreview;

        public BuildingViewStyle Style => style;

        public void Initialize(Match match)
        {
            this.match = match;
            radiusPreview = CreatePreview("Build Radius", style.CircleSprite, style.BodySortingOrder - 2);
            sitePreview = CreatePreview("Barracks Site", style.SquareSprite, style.OverlaySortingOrder);
            sitePreview.transform.localScale = new Vector3(style.BarracksSize.x, style.BarracksSize.y, 1f);
            campMarker = CreatePreview("Camp Selection", squadViews.Style.MarkerSprite, style.BodySortingOrder + 4);
            campMarker.color = squadViews.Style.SelectionColor;
            campMarker.enabled = false;
            HidePlacement();
        }

        /// <summary>Выделенная группа своей толпы рекрутов (род войск) подсвечивается рамкой, как отряд; null — снять.</summary>
        public void SetSelectedCamp(Side side, UnitType? type)
        {
            selectedCampSide = side;
            selectedCampType = type;
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
                CreateSentries(building);
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
                    if (sentries.TryGetValue(id, out Sentries posts))
                    {
                        foreach (SpriteRenderer figure in posts.Figures)
                        {
                            Destroy(figure.gameObject);
                        }

                        foreach (SpriteRenderer arms in posts.Arms)
                        {
                            if (arms != null)
                            {
                                Destroy(arms.gameObject);
                            }
                        }

                        sentries.Remove(id);
                    }
                }
            }

            // На здание идёт штурм, если хоть один отряд получил приказ его атаковать.
            assaulted.Clear();
            foreach (Squad squad in match.Squads)
            {
                if (squad.Order.Type == SquadOrderType.Assault)
                {
                    assaulted.Add(squad.Order.TargetBuildingId);
                }
            }

            foreach (BuildingView view in views.Values)
            {
                view.Render(match, assaulted.Contains(view.Building.Id));
            }

            RenderSentries();
            RenderWorkers();

            campMarker.enabled = false;
            foreach (Side side in new[] { Side.France, Side.Austria })
            {
                PlayerState player = match.GetPlayer(side);
                // Пехота ждёт двумя группами в своём обмундировании: фузилёры слева от середины толпы, гренадёры справа.
                float groupOffset = math.max(1, style.RecruitsPerRow) * style.RecruitsSpacing.x * 0.5f + style.RecruitsGroupGap * 0.5f;
                RenderWaiting(side, UnitType.Fusiliers, player.FusilierReserve, fusiliers[(int)side], 1f, -groupOffset);
                RenderWaiting(side, UnitType.Grenadiers, player.GrenadierReserve, grenadiers[(int)side], 1f, groupOffset);
                RenderWaiting(side, UnitType.Cavalry, player.CavalryReserve, riders[(int)side], 1.8f, 0f);
                CollectCampFigures(side);
                RenderCampAims(side, false);
                RenderCampAims(side, true);
            }

            RenderLeaving();
        }

        /// <summary>Рамка вокруг мест группы толпы (в осях толпы: <paramref name="right"/> — поперёк, <paramref name="front"/> — вперёд).</summary>
        private void PlaceCampMarker(float2 start, float2 right, float2 front, float2 min, float2 max, Quaternion rotation)
        {
            float padding = squadViews.Style.SelectionPadding;
            float2 mid = (min + max) * 0.5f;
            float2 center = start + right * mid.x + front * mid.y;
            campMarker.enabled = true;
            campMarker.transform.SetPositionAndRotation(new Vector3(center.x, center.y, 0f), rotation);
            campMarker.transform.localScale = new Vector3(max.x - min.x + padding * 2f + 0.5f, max.y - min.y + padding * 2f + 0.5f, 1f);
        }

        /// <summary>Часовые, давшие залп за тик, поворачиваются к цели.</summary>
        public void PlaySentries(IReadOnlyList<SentryVolley> volleys)
        {
            foreach (SentryVolley volley in volleys)
            {
                if (sentries.TryGetValue(volley.BuildingId, out Sentries posts))
                {
                    posts.AlertUntil = Time.time + style.SentryAlertTime;
                    posts.Target = volley.TargetPosition;
                }
            }
        }

        private void CreateSentries(Building building)
        {
            Vector2[] postsShape = style.SentryPosts;
            int count = math.min(match.Settings.buildings.sentryShots, postsShape?.Length ?? 0);
            Sprite sprite = squadViews.Style.GetSoldierSprite(building.Owner, UnitType.Fusiliers, SoldierRole.Private);
            if (count <= 0 || sprite == null)
            {
                return;
            }

            var posts = new Sentries { Figures = new SpriteRenderer[count], Arms = new SpriteRenderer[count], Stand = sprite };
            bool canPose = squadViews.Style.TryGetMeleeSprites(building.Owner, UnitType.Fusiliers, SoldierRole.Private, out Sprite body, out Sprite armsSprite);
            posts.Body = canPose ? body : null;
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer figure = CreatePreview($"Sentry {building.Id} {i}", sprite, style.BodySortingOrder + 5);
                figure.transform.localScale = Vector3.one * (squadViews.Style.SoldierScale * style.SentryScale);
                posts.Figures[i] = figure;
                if (canPose)
                {
                    SpriteRenderer arms = CreatePreview($"Sentry Arms {building.Id} {i}", armsSprite, style.BodySortingOrder + 6);
                    arms.transform.localScale = figure.transform.localScale;
                    arms.enabled = false;
                    posts.Arms[i] = arms;
                }
            }

            sentries.Add(building.Id, posts);
        }

        /// <summary>
        /// Часовые стоят на постах построенного здания и смотрят наружу. Перед залпом вскидывают ружья к цели
        /// («Целься!» — видно, откуда сейчас ударят), после залпа опускают и какое-то время смотрят на цель.
        /// </summary>
        private void RenderSentries()
        {
            SquadViewStyle squadStyle = squadViews.Style;
            foreach (KeyValuePair<int, Sentries> pair in sentries)
            {
                Building building = match.FindBuilding(pair.Key);
                bool shown = building != null && building.IsBuilt && !building.IsDestroyed;
                Sentries posts = pair.Value;
                Squad aimed = shown && building.IsSentryAiming ? match.FindSquad(building.SentryAimTargetId) : null;
                if (aimed != null)
                {
                    posts.Target = aimed.Position;
                    posts.AlertUntil = math.max(posts.AlertUntil, Time.time + style.SentryAlertTime);
                }

                posts.AimBlend = StepBlend(posts.AimBlend, aimed != null, squadStyle);
                Vector2 size = style.SizeOf(building != null ? building.Type : BuildingType.Barracks);
                float direction = building != null && building.Position.x > 0f ? -1f : 1f;
                bool alert = Time.time < posts.AlertUntil;
                bool posed = posts.Body != null && posts.AimBlend > 0f;
                for (int i = 0; i < posts.Figures.Length; i++)
                {
                    SpriteRenderer figure = posts.Figures[i];
                    SpriteRenderer arms = posts.Arms[i];
                    figure.enabled = shown;
                    if (arms != null)
                    {
                        arms.enabled = shown && posed;
                    }

                    if (!shown)
                    {
                        continue;
                    }

                    float2 position = GetSentryPosition(building, i, size, direction);
                    float2 look = alert ? posts.Target - position : position - building.Position;
                    Quaternion rotation = FacingOf(look);
                    figure.transform.SetPositionAndRotation(new Vector3(position.x, position.y, 0f), rotation);
                    Sprite sprite = posed ? posts.Body : posts.Stand;
                    if (figure.sprite != sprite)
                    {
                        figure.sprite = sprite;
                    }

                    if (posed)
                    {
                        float2 reach = math.normalizesafe(look) * (squadStyle.AimReach * math.smoothstep(0f, 1f, posts.AimBlend));
                        arms.transform.SetPositionAndRotation(new Vector3(position.x + reach.x, position.y + reach.y, 0f), rotation);
                    }
                }
            }
        }

        private float2 GetSentryPosition(Building building, int index, Vector2 size, float direction)
        {
            Vector2 post = style.SentryPosts[index];
            return building.Position + new float2(post.x * size.x * direction, post.y * size.y);
        }

        /// <summary>
        /// Где стоят часовые здания (для дыма их залпа); пусто — часовых не рисуют (тогда дым — у стены здания).
        /// </summary>
        public void GetSentryMuzzles(int buildingId, List<float2> positions)
        {
            positions.Clear();
            Building building = match.FindBuilding(buildingId);
            if (building == null || !sentries.TryGetValue(buildingId, out Sentries posts))
            {
                return;
            }

            Vector2 size = style.SizeOf(building.Type);
            float direction = building.Position.x > 0f ? -1f : 1f;
            for (int i = 0; i < posts.Figures.Length; i++)
            {
                positions.Add(GetSentryPosition(building, i, size, direction));
            }
        }

        /// <summary>Плавное «вскинуть ружьё — опустить» за aimRaiseTime / aimLowerTime.</summary>
        private static float StepBlend(float blend, bool aiming, SquadViewStyle squadStyle)
        {
            float time = aiming ? squadStyle.AimRaiseTime : squadStyle.AimLowerTime;
            float step = time > 0f ? Time.deltaTime / time : 1f;
            return aiming ? math.min(1f, blend + step) : math.max(0f, blend - step);
        }

        /// <summary>Видимые фигурки толпы стороны в постоянном порядке: пехота — фузилёры, потом гренадёры; конница отдельно.</summary>
        private void CollectCampFigures(Side side)
        {
            List<Waiter> infantry = infantryCampFigures[(int)side];
            infantry.Clear();
            AddShown(fusiliers[(int)side], infantry);
            AddShown(grenadiers[(int)side], infantry);
            List<Waiter> cavalry = cavalryCampFigures[(int)side];
            cavalry.Clear();
            AddShown(riders[(int)side], cavalry);
        }

        private static void AddShown(List<Waiter> crowd, List<Waiter> into)
        {
            foreach (Waiter waiter in crowd)
            {
                if (waiter.Shown)
                {
                    into.Add(waiter);
                }
            }
        }

        /// <summary>Кто из толпы стреляет выстрелом с этим номером: номер разбрасывается по толпе, а не идёт подряд.</summary>
        private static Waiter PickShooter(List<Waiter> figures, int shooterIndex)
        {
            if (figures.Count == 0)
            {
                return null;
            }

            int pick = (int)(Hash01(shooterIndex * 7 + 12345) * figures.Count);
            return figures[math.min(pick, figures.Count - 1)];
        }

        /// <summary>
        /// «Целься!» в толпе: тот, чей выстрел вразнобой готовится, поворачивается к ближайшему врагу и выносит ружьё вперёд;
        /// после выстрела опускает его и снова смотрит вперёд.
        /// </summary>
        private void RenderCampAims(Side side, bool cavalry)
        {
            List<Waiter> figures = cavalry ? cavalryCampFigures[(int)side] : infantryCampFigures[(int)side];
            Squad camp = FindCamp(side, cavalry);
            aimingWaiters.Clear();
            if (camp != null)
            {
                foreach (SkirmishAim aim in camp.SkirmishAims)
                {
                    Waiter shooter = PickShooter(figures, aim.ShooterIndex);
                    if (shooter != null)
                    {
                        aimingWaiters.Add(shooter);
                    }
                }
            }

            Squad enemy = camp != null && aimingWaiters.Count > 0 ? FindNearestEnemy(camp) : null;
            SquadViewStyle squadStyle = squadViews.Style;
            foreach (Waiter waiter in figures)
            {
                bool aiming = enemy != null && aimingWaiters.Contains(waiter) && !waiter.OnPath;
                waiter.AimBlend = StepBlend(waiter.AimBlend, aiming, squadStyle);
                bool posed = waiter.AimBlend > 0f && waiter.Body != null && waiter.Arms != null;
                if (waiter.Arms != null && waiter.Arms.enabled != posed)
                {
                    waiter.Arms.enabled = posed;
                }

                if (waiter.AimBlend <= 0f)
                {
                    continue;
                }

                if (aiming)
                {
                    waiter.AimTarget = enemy.Position;
                }

                float2 look = waiter.AimTarget - waiter.Position;
                Quaternion rotation = FacingOf(look);
                waiter.Renderer.transform.rotation = rotation;
                if (!posed)
                {
                    continue;
                }

                waiter.Renderer.sprite = waiter.Body;
                float2 reach = math.normalizesafe(look) * (squadStyle.AimReach * math.smoothstep(0f, 1f, waiter.AimBlend));
                Vector3 at = waiter.Renderer.transform.position;
                waiter.Arms.transform.SetPositionAndRotation(new Vector3(at.x + reach.x, at.y + reach.y, 0f), rotation);
            }
        }

        private Squad FindNearestEnemy(Squad camp)
        {
            Squad best = null;
            float bestDistance = float.MaxValue;
            foreach (Squad squad in match.Squads)
            {
                if (squad.Side == camp.Side || squad.Alive <= 0 || squad.IsRouting)
                {
                    continue;
                }

                float distance = math.distancesq(squad.Position, camp.Position);
                if (distance < bestDistance)
                {
                    best = squad;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>
        /// Откуда выстрелил человек толпы с этим номером выстрела вразнобой — для дыма у него, а не по всей толпе.
        /// false — толпу не рисуют.
        /// </summary>
        public bool TryGetCampShooter(Squad camp, int shooterIndex, out float2 position)
        {
            List<Waiter> figures = camp.IsCavalryCamp ? cavalryCampFigures[(int)camp.Side] : infantryCampFigures[(int)camp.Side];
            Waiter shooter = shooterIndex >= 0 ? PickShooter(figures, shooterIndex) : null;
            position = shooter != null ? shooter.Position : camp.Position;
            return shooter != null;
        }

        /// <summary>
        /// Пока здание чинят, у него работают рабочие из штаба: выходят из ворот штаба, становятся у стены со стороны
        /// штаба и «стучат молотками»; когда починка кончилась (или прервалась), возвращаются в штаб и исчезают.
        /// </summary>
        private void RenderWorkers()
        {
            Sprite sprite = squadViews.Style.RecruitSprite;
            if (sprite == null)
            {
                return;
            }

            foreach (Building building in match.Buildings)
            {
                if (!building.IsRepairing || crews.ContainsKey(building.Id))
                {
                    continue;
                }

                Building headquarters = match.GetHeadquarters(building.Owner);
                if (headquarters == null)
                {
                    continue;
                }

                float2 gate = GetHeadquartersGate(headquarters);
                var crew = new List<Worker>();
                for (int i = 0; i < style.WorkersPerBuilding; i++)
                {
                    SpriteRenderer renderer = CreatePreview($"Worker {building.Id} {i}", sprite, style.BodySortingOrder + 5);
                    renderer.color = style.WorkerColor;
                    renderer.transform.localScale = Vector3.one * squadViews.Style.SoldierScale;
                    crew.Add(new Worker
                    {
                        Renderer = renderer,
                        Position = gate,
                        Post = GetWorkerPost(building, gate, i),
                        Step = new WalkerStep(building.Id * 31 + i)
                    });
                }

                crews.Add(building.Id, crew);
            }

            float step = style.WorkerSpeed * Time.deltaTime;
            finishedCrews.Clear();
            foreach (KeyValuePair<int, List<Worker>> pair in crews)
            {
                Building building = match.FindBuilding(pair.Key);
                Building headquarters = building != null ? match.GetHeadquarters(building.Owner) : null;
                bool working = building != null && building.IsRepairing;
                float2 home = headquarters != null ? GetHeadquartersGate(headquarters) : pair.Value[0].Position;
                bool allHome = true;
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    Worker worker = pair.Value[i];
                    worker.Returning |= !working;
                    float2 goal = worker.Returning ? home : worker.Post;
                    float2 toGoal = goal - worker.Position;
                    float distance = math.length(toGoal);
                    float2 look = toGoal;
                    float2 shown = worker.Position;
                    bool walking = distance > step && distance > 1e-3f;
                    if (walking)
                    {
                        worker.Position += toGoal / distance * step;
                        shown = worker.Position;
                    }
                    else
                    {
                        worker.Position = goal;
                        shown = goal;
                        if (!worker.Returning)
                        {
                            // У стены: лицом к зданию, качается, будто стучит молотком.
                            look = building.Position - goal;
                            float2 toWall = math.normalizesafe(look);
                            shown = goal + toWall * (math.sin(Time.time * style.WorkerBobRate + i * 1.7f) * style.WorkerBob);
                        }
                    }

                    allHome &= worker.Returning && distance <= step;
                    worker.Step.Apply(worker.Renderer, shown, FacingOf(look), walking, look, null, null, null, squadViews.Style);
                }

                if (allHome || headquarters == null)
                {
                    finishedCrews.Add(pair.Key);
                }
            }

            foreach (int id in finishedCrews)
            {
                foreach (Worker worker in crews[id])
                {
                    Destroy(worker.Renderer.gameObject);
                }

                crews.Remove(id);
            }
        }

        /// <summary>Ворота штаба — середина стороны, обращённой к центру карты.</summary>
        private float2 GetHeadquartersGate(Building headquarters)
        {
            float direction = headquarters.Position.x > 0f ? -1f : 1f;
            return headquarters.Position + new float2(direction * style.HeadquartersSize.x * 0.5f, 0f);
        }

        /// <summary>Место рабочего у стены здания со стороны штаба; рабочие стоят веером через workerSpread градусов.</summary>
        private float2 GetWorkerPost(Building building, float2 gate, int index)
        {
            Vector2 size = style.SizeOf(building.Type);
            float2 toGate = math.normalizesafe(gate - building.Position, new float2(1f, 0f));
            float angle = math.radians((index - (style.WorkersPerBuilding - 1) * 0.5f) * style.WorkerSpread);
            float2 direction = new float2(
                toGate.x * math.cos(angle) - toGate.y * math.sin(angle),
                toGate.x * math.sin(angle) + toGate.y * math.cos(angle));
            // Луч из центра до края прямоугольника здания и чуть дальше.
            float reach = math.min(
                math.abs(direction.x) > 1e-3f ? size.x * 0.5f / math.abs(direction.x) : float.MaxValue,
                math.abs(direction.y) > 1e-3f ? size.y * 0.5f / math.abs(direction.y) : float.MaxValue);
            return building.Position + direction * (reach + style.WorkerStandOff);
        }

        private static Quaternion FacingOf(float2 direction)
        {
            return math.lengthsq(direction) < 1e-6f
                ? Quaternion.identity
                : Quaternion.Euler(0f, 0f, math.degrees(math.atan2(direction.y, direction.x)) - 90f);
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
                        DestroyWaiter(waiter);
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

        private static void DestroyWaiter(Waiter waiter)
        {
            Destroy(waiter.Renderer.gameObject);
            if (waiter.Arms != null)
            {
                Destroy(waiter.Arms.gameObject);
            }
        }

        private void RenderLeaving()
        {
            float step = style.RecruitWalkSpeed * Time.deltaTime;
            for (int i = leaving.Count - 1; i >= 0; i--)
            {
                Waiter waiter = leaving[i];
                waiter.AimBlend = 0f;
                if (waiter.Arms != null)
                {
                    waiter.Arms.enabled = false;
                }

                Walk(waiter, waiter.Position, step, waiter.Renderer.transform.rotation);
                if (!waiter.OnPath)
                {
                    DestroyWaiter(waiter);
                    leaving.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Шаг к цели: по точкам пути, если они остались, иначе к <paramref name="target"/>. Идущий смотрит,
        /// куда идёт; дошедший до места — в <paramref name="standing"/>.
        /// </summary>
        private void Walk(Waiter waiter, float2 target, float step, Quaternion standing)
        {
            float2 goal = waiter.OnPath ? waiter.NextPoint : target;
            float2 toGoal = goal - waiter.Position;
            float distance = math.length(toGoal);
            Quaternion facing = standing;
            bool walking = distance > step && distance > 1e-3f;
            if (walking)
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
                    walking = true;
                }
            }

            waiter.Stride.Apply(waiter.Renderer, waiter.Position, facing, walking, toGoal,
                waiter.Stand, waiter.Walk0, waiter.Walk1, squadViews.Style);
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
                var waiter = new Waiter
                {
                    Renderer = person,
                    Stride = new WalkerStep((int)side * 1000 + (int)type * 100 + crowd.Count),
                    Stand = sprite,
                    Walk0 = squadViews.Style.GetWalkSprite(side, type, SoldierRole.Private, 0),
                    Walk1 = squadViews.Style.GetWalkSprite(side, type, SoldierRole.Private, 1)
                };
                if (type != UnitType.Cavalry
                    && squadViews.Style.TryGetMeleeSprites(side, type, SoldierRole.Private, out Sprite body, out Sprite armsSprite))
                {
                    waiter.Body = body;
                    waiter.Arms = CreatePreview($"Waiting Arms {type} {side} {crowd.Count}", armsSprite, style.BodySortingOrder + 6);
                    waiter.Arms.transform.localScale = person.transform.localScale;
                    waiter.Arms.enabled = false;
                }

                crowd.Add(waiter);
            }

            for (int i = shown; i < crowd.Count; i++)
            {
                crowd[i].Renderer.enabled = false;
                crowd[i].Shown = false;
                crowd[i].AimBlend = 0f;
                if (crowd[i].Arms != null)
                {
                    crowd[i].Arms.enabled = false;
                }
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
            bool marked = selectedCampType == type && selectedCampSide == side;
            float2 markMin = new float2(float.MaxValue, float.MaxValue);
            float2 markMax = new float2(float.MinValue, float.MinValue);
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
                markMin = math.min(markMin, local);
                markMax = math.max(markMax, local);
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

            if (marked)
            {
                PlaceCampMarker(start, right, front, markMin, markMax, rotation);
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
