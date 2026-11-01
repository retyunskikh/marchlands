using System.Collections.Generic;
using Marchlands.Input;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Marchlands.UI;
using Unity.Mathematics;
using UnityEngine;

namespace Marchlands.Bootstrap
{
    /// <summary>
    /// Обучение: пошаговые уроки. Каждый шаг — подсказка с заданием; следующий открывается, когда игрок выполнил
    /// задание (смотрим на состояние боя), а у шагов-пояснений — по «Далее». Противник включается по ходу: на уроке
    /// залпа выходят его отряды, на последнем шаге — полный ИИ. Только читает бой: приказы отдаёт сам игрок.
    /// </summary>
    public sealed class TutorialDirector : MonoBehaviour
    {
        // Шаги обучения по порядку (тексты — в messages, с тем же номером).
        private enum Step
        {
            Welcome,
            Camera,
            Select,
            Move,
            Rotate,
            Column,
            Village,
            Volley,
            Morale,
            Charge,
            Barracks,
            Hiring,
            Forming,
            Goal
        }

        [SerializeField] private TutorialPanel panel;
        [Tooltip("Тексты уроков по шагам (Welcome, Camera, Select, Move, Rotate, Column, Village, Volley, Morale, Charge, Barracks, Hiring, Forming, Goal).")]
        [SerializeField, TextArea(2, 4)] private string[] messages =
        {
            "Добро пожаловать! Вы командуете французами (синие). Нажмите «Далее».",
            "Сдвиньте карту пальцем. Двумя пальцами — приблизьте или отдалите.",
            "Тапните по своему отряду — он обведён кругом.",
            "Проведите пальцем от отряда в любую точку поля — отряд пойдёт туда.",
            "Потяните за кольцо вокруг отряда — он развернётся. Линия стреляет только вперёд, в жёлтый сектор.",
            "Нажмите «Марш» — отряд встанет в колонну (быстрее идёт, почти не стреляет). «В линию» — обратно.",
            "Отправьте отряд в обведённую деревню и стойте там, пока флаг не станет синим. Деревни дают золото.",
            "Враг идёт на вас (обведён)! Разверните к нему линию. Когда он войдёт в жёлтый сектор — нажмите «Залп!».",
            "Отряд перезаряжается — кольцо за ним заполняется. Полоска над отрядом — боевой дух: при потерях он падает, и отряд бежит. Нажмите «Далее».",
            "Нажмите «В штыки» и тапните по обведённому врагу. Удар во фланг или в тыл сильнее.",
            "Тапните по своему штабу (обведён), нажмите «Казарма», тапните место рядом со штабом и нажмите «Поставить».",
            "Тапните по казарме (обведена), когда она достроится, и включите найм фузилёров.",
            "Рекруты собираются у казармы. Когда их хватит на отряд, нажмите его кружок слева вверху. Нажмите «Далее».",
            "Захватите все деревни или разрушьте вражеский штаб: выделите отряд и тапните по вражескому зданию. Дальше — сами. Удачи!"
        };

        [Tooltip("Тексты уроков для ПК (мышь и клавиатура), по тем же шагам.")]
        [SerializeField, TextArea(2, 4)] private string[] desktopMessages =
        {
            "Добро пожаловать! Вы командуете французами (синие). Нажмите «Далее».",
            "Сдвиньте карту: клавиши WASD или стрелки, мышь у края экрана или средняя кнопка. Колесо — приблизить или отдалить.",
            "Щёлкните левой кнопкой по своему отряду — он обведён кругом.",
            "Щёлкните правой кнопкой в любую точку поля — отряд пойдёт туда. Зажмите правую и потяните — он встанет фронтом в ту сторону.",
            "Потяните левой кнопкой за кольцо вокруг отряда — он развернётся. Линия стреляет только вперёд, в жёлтый сектор.",
            "Нажмите «Марш» (R) — отряд встанет в колонну (быстрее идёт, почти не стреляет). «В линию» — обратно.",
            "Отправьте отряд в обведённую деревню и стойте там, пока флаг не станет синим. Деревни дают золото.",
            "Враг идёт на вас (обведён)! Разверните к нему линию. Когда он войдёт в жёлтый сектор — нажмите «Залп!» (F).",
            "Отряд перезаряжается — кольцо за ним заполняется. Полоска над отрядом — боевой дух: при потерях он падает, и отряд бежит. Нажмите «Далее».",
            "Нажмите «В штыки» (C) и щёлкните по обведённому врагу. Удар во фланг или в тыл сильнее.",
            "Щёлкните по своему штабу (обведён), нажмите «Казарма», выберите место рядом со штабом и нажмите «Поставить».",
            "Щёлкните по казарме (обведена), когда она достроится, и включите найм фузилёров.",
            "Рекруты собираются у казармы. Когда их хватит на отряд, нажмите его кружок слева вверху. Нажмите «Далее».",
            "Захватите все деревни или разрушьте вражеский штаб: выделите отряд и щёлкните правой кнопкой по вражескому зданию. Дальше — сами. Удачи!"
        };

        [Header("Подсветка кнопок")]
        [Tooltip("Рамка вокруг кнопки, которую нужно нажать на этом шаге.")]
        [SerializeField] private Sprite buttonFrameSprite;
        [SerializeField] private Color buttonFrameColor = new Color(1f, 0.85f, 0.2f, 1f);
        [Tooltip("Насколько рамка шире кнопки, пикселей холста.")]
        [SerializeField] private float buttonFramePadding = 10f;
        [SerializeField] private RectTransform formationButton;
        [SerializeField] private RectTransform volleyButton;
        [SerializeField] private RectTransform chargeButton;
        [SerializeField] private RectTransform buildBarracksButton;
        [SerializeField] private RectTransform placeButton;
        [SerializeField] private RectTransform hireFusiliersButton;
        [SerializeField] private RectTransform formFusiliersButton;

        [Tooltip("На сколько нужно сдвинуть карту (u) или изменить приближение, чтобы урок камеры засчитался.")]
        [SerializeField] private float cameraMoveToPass = 4f;
        [Tooltip("На сколько нужно уйти отряду (u) или развернуться (градусы), чтобы урок засчитался.")]
        [SerializeField] private float moveToPass = 3f;
        [SerializeField] private float turnToPass = 25f;

        [Header("Подсветка цели шага")]
        [Tooltip("Кольцо вокруг того, с чем работать на этом шаге (отряд, деревня, враг, штаб, казарма).")]
        [SerializeField] private Sprite highlightSprite;
        [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.2f, 0.95f);
        [Tooltip("Запас кольца вокруг цели, u, и сколько раз в секунду оно пульсирует.")]
        [SerializeField] private float highlightPadding = 2f;
        [SerializeField] private float highlightPulseRate = 1.5f;
        [SerializeField] private float highlightPulse = 0.12f;
        [SerializeField] private int highlightSortingOrder = 300;

        private Match match;
        private Side side;
        private BattleInputController input;
        private Camera view;
        private GatedController enemyTactics;
        private GatedController enemyStrategy;

        private int step = -1;
        private SpriteRenderer highlight;
        private UnityEngine.UI.Image buttonFrame;

        private string[] Messages => Marchlands.View.Platform.IsDesktop && desktopMessages != null && desktopMessages.Length == messages.Length
            ? desktopMessages
            : messages;
        private Squad selected;
        private Vector3 cameraStart;
        private float zoomStart;
        private readonly Dictionary<int, float2> positionsAtStart = new Dictionary<int, float2>();
        private readonly Dictionary<int, float> facingsAtStart = new Dictionary<int, float>();

        public void Initialize(Match match, Side side, BattleInputController input, GatedController enemyTactics, GatedController enemyStrategy)
        {
            this.match = match;
            this.side = side;
            this.input = input;
            this.enemyTactics = enemyTactics;
            this.enemyStrategy = enemyStrategy;
            view = Camera.main;
            input.SelectionChanged += OnSelectionChanged;
            panel.NextPressed += OnNext;
            panel.SkipPressed += Finish;
            Go(0);
        }

        private void OnDestroy()
        {
            if (input != null)
            {
                input.SelectionChanged -= OnSelectionChanged;
            }
        }

        private void OnSelectionChanged(Squad squad)
        {
            selected = squad;
        }

        private void Update()
        {
            if (match == null || step < 0 || step >= messages.Length)
            {
                return;
            }

            // Игрок ушёл вперёд уроков (все деревни или уже казарма) — противник воюет в полную силу.
            if (match.CountVillages(side) >= match.Villages.Count || HasOwn(b => b.Type == BuildingType.Barracks))
            {
                enemyTactics.Active = true;
                enemyStrategy.Active = true;
            }

            if (IsDone((Step)step))
            {
                Go(step + 1);
                return;
            }

            UpdateHighlight((Step)step);
            UpdateButtonFrame((Step)step);
        }

        /// <summary>
        /// Рамка вокруг кнопки, которую пора нажать: «Марш», «Залп!», «В штыки», «Казарма», «Поставить», найм фузилёров,
        /// кружок нового отряда. Рамка — дочерний объект кнопки: видна, только пока видна кнопка.
        /// </summary>
        private void UpdateButtonFrame(Step current)
        {
            RectTransform target = ButtonFor(current);
            if (target == null || buttonFrameSprite == null)
            {
                if (buttonFrame != null)
                {
                    buttonFrame.enabled = false;
                }

                return;
            }

            if (buttonFrame == null)
            {
                var frameObject = new GameObject("Tutorial Button Frame", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                buttonFrame = frameObject.GetComponent<UnityEngine.UI.Image>();
                buttonFrame.sprite = buttonFrameSprite;
                buttonFrame.type = UnityEngine.UI.Image.Type.Sliced;
                buttonFrame.raycastTarget = false;
                buttonFrame.color = buttonFrameColor;
            }

            var rect = (RectTransform)buttonFrame.transform;
            if (rect.parent != target)
            {
                rect.SetParent(target, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
            }

            rect.SetAsLastSibling();
            float pulse = buttonFramePadding * (1f + 0.5f * Mathf.Sin(Time.unscaledTime * highlightPulseRate * Mathf.PI * 2f));
            rect.offsetMin = new Vector2(-pulse, -pulse);
            rect.offsetMax = new Vector2(pulse, pulse);
            buttonFrame.enabled = true;
        }

        private RectTransform ButtonFor(Step current)
        {
            switch (current)
            {
                case Step.Column:
                    return formationButton;
                case Step.Volley:
                    return volleyButton;
                case Step.Charge:
                    return chargeButton;
                case Step.Barracks:
                    // Сначала — кнопка «Казарма» в карточке штаба, потом — «Поставить».
                    return placeButton != null && placeButton.gameObject.activeInHierarchy ? placeButton : buildBarracksButton;
                case Step.Hiring:
                    return hireFusiliersButton;
                case Step.Forming:
                    return formFusiliersButton;
                default:
                    return null;
            }
        }

        /// <summary>Кольцо вокруг цели шага: свой отряд, деревня, ближайший враг, штаб, казарма.</summary>
        private void UpdateHighlight(Step current)
        {
            float2 at = float2.zero;
            float radius = 0f;
            bool show = TryGetHighlight(current, ref at, ref radius);
            if (highlight == null)
            {
                if (!show || highlightSprite == null)
                {
                    return;
                }

                var ring = new GameObject("Tutorial Highlight");
                highlight = ring.AddComponent<SpriteRenderer>();
                highlight.sprite = highlightSprite;
                highlight.color = highlightColor;
                highlight.sortingOrder = highlightSortingOrder;
            }

            highlight.enabled = show;
            if (!show)
            {
                return;
            }

            float pulse = 1f + highlightPulse * Mathf.Sin(Time.unscaledTime * highlightPulseRate * Mathf.PI * 2f);
            float size = (radius + highlightPadding) * 2f * pulse / Mathf.Max(0.01f, highlightSprite.bounds.size.x);
            highlight.transform.position = new Vector3(at.x, at.y, 0f);
            highlight.transform.localScale = new Vector3(size, size, 1f);
        }

        private bool TryGetHighlight(Step current, ref float2 at, ref float radius)
        {
            switch (current)
            {
                case Step.Select:
                    foreach (Squad squad in OwnSquads())
                    {
                        at = squad.Position;
                        radius = math.length(FormationLayout.GetHalfExtents(squad, match.Settings.squads));
                        return true;
                    }

                    return false;
                case Step.Village:
                    return TryNearestVillage(ref at, ref radius);
                case Step.Volley:
                case Step.Charge:
                    return TryNearestEnemy(ref at, ref radius);
                case Step.Barracks:
                    return TryOwnBuilding(BuildingType.Headquarters, ref at, ref radius);
                case Step.Hiring:
                    return TryOwnBuilding(BuildingType.Barracks, ref at, ref radius);
                default:
                    return false;
            }
        }

        private bool TryNearestVillage(ref float2 at, ref float radius)
        {
            float2 from = AnyOwnPosition();
            float best = float.MaxValue;
            foreach (Simulation.Economy.Village village in match.Villages)
            {
                if (village.Owner == side)
                {
                    continue;
                }

                float distance = math.distance(from, village.Position);
                if (distance < best)
                {
                    best = distance;
                    at = village.Position;
                    radius = match.Settings.economy.captureRadius;
                }
            }

            return best < float.MaxValue;
        }

        private bool TryNearestEnemy(ref float2 at, ref float radius)
        {
            float2 from = AnyOwnPosition();
            float best = float.MaxValue;
            foreach (Squad squad in match.Squads)
            {
                if (squad.Side == side || squad.IsCamp || squad.IsRouting || squad.Alive <= 0)
                {
                    continue;
                }

                float distance = math.distance(from, squad.Position);
                if (distance < best)
                {
                    best = distance;
                    at = squad.Position;
                    radius = math.length(FormationLayout.GetHalfExtents(squad, match.Settings.squads));
                }
            }

            return best < float.MaxValue;
        }

        private bool TryOwnBuilding(BuildingType type, ref float2 at, ref float radius)
        {
            foreach (Building building in match.Buildings)
            {
                if (building.Owner == side && building.Type == type)
                {
                    at = building.Position;
                    radius = HighlightBuildingRadius;
                    return true;
                }
            }

            return false;
        }

        // Радиус кольца вокруг здания (штаба, казармы), u.
        private const float HighlightBuildingRadius = 4.5f;

        private float2 AnyOwnPosition()
        {
            foreach (Squad squad in OwnSquads())
            {
                return squad.Position;
            }

            return float2.zero;
        }

        private void OnNext()
        {
            if (step >= 0 && WaitsForNext((Step)step))
            {
                Go(step + 1);
            }
        }

        private static bool WaitsForNext(Step step)
        {
            return step == Step.Welcome || step == Step.Morale || step == Step.Forming || step == Step.Goal;
        }

        private void Go(int next)
        {
            step = next;
            if (step >= messages.Length)
            {
                Finish();
                return;
            }

            Enter((Step)step);
            panel.Show(Messages[step], WaitsForNext((Step)step), step, messages.Length);
        }

        /// <summary>Начало шага: запомнить, от чего считать выполнение, и включить противника, когда пора.</summary>
        private void Enter(Step current)
        {
            cameraStart = view != null ? view.transform.position : Vector3.zero;
            zoomStart = view != null ? view.orthographicSize : 0f;
            positionsAtStart.Clear();
            facingsAtStart.Clear();
            foreach (Squad squad in OwnSquads())
            {
                positionsAtStart[squad.Id] = squad.Position;
                facingsAtStart[squad.Id] = squad.Facing;
            }

            // Противник включается по ходу: с урока о деревнях спорит за них и воюет, с урока о казарме — нанимает и строит.
            if (current >= Step.Village)
            {
                enemyTactics.Active = true;
            }

            if (current >= Step.Barracks)
            {
                enemyStrategy.Active = true;
            }
        }

        private bool IsDone(Step current)
        {
            switch (current)
            {
                case Step.Camera:
                    return view != null && (Vector3.Distance(view.transform.position, cameraStart) > cameraMoveToPass
                        || Mathf.Abs(view.orthographicSize - zoomStart) > cameraMoveToPass * 0.5f);
                case Step.Select:
                    return selected != null && selected.Side == side;
                case Step.Move:
                    foreach (Squad squad in OwnSquads())
                    {
                        if (positionsAtStart.TryGetValue(squad.Id, out float2 start) && math.distance(start, squad.Position) > moveToPass)
                        {
                            return true;
                        }
                    }

                    return false;
                case Step.Rotate:
                    foreach (Squad squad in OwnSquads())
                    {
                        if (facingsAtStart.TryGetValue(squad.Id, out float start) && math.abs(Angles.Delta(start, squad.Facing)) > turnToPass)
                        {
                            return true;
                        }
                    }

                    return false;
                case Step.Column:
                    foreach (Squad squad in OwnSquads())
                    {
                        if (squad.TargetFormation == Formation.Column)
                        {
                            return true;
                        }
                    }

                    return false;
                case Step.Village:
                    return match.CountVillages(side) > 0;
                case Step.Volley:
                    foreach (Squad squad in OwnSquads())
                    {
                        if (!squad.IsArtillery && squad.FireState == FireState.Reloading)
                        {
                            return true;
                        }
                    }

                    return false;
                case Step.Charge:
                    foreach (Squad squad in OwnSquads())
                    {
                        if (squad.Order.Type == SquadOrderType.Charge || squad.IsInMelee)
                        {
                            return true;
                        }
                    }

                    return false;
                case Step.Barracks:
                    return HasOwn(b => b.Type == BuildingType.Barracks);
                case Step.Hiring:
                    return HasOwn(b => b.Type == BuildingType.Barracks && b.IsHiringAny);
                default:
                    return false;
            }
        }

        /// <summary>Обучение пройдено или пропущено: подсказки скрыты, противник воюет в полную силу.</summary>
        private void Finish()
        {
            step = messages.Length;
            panel.Hide();
            if (highlight != null)
            {
                highlight.enabled = false;
            }

            if (buttonFrame != null)
            {
                buttonFrame.enabled = false;
            }

            enemyTactics.Active = true;
            enemyStrategy.Active = true;
        }

        private IEnumerable<Squad> OwnSquads()
        {
            foreach (Squad squad in match.Squads)
            {
                if (squad.Side == side && !squad.IsCamp && !squad.IsRouting)
                {
                    yield return squad;
                }
            }
        }

        private bool HasOwn(System.Func<Building, bool> predicate)
        {
            foreach (Building building in match.Buildings)
            {
                if (building.Owner == side && predicate(building))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
