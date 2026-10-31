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
            "Добро пожаловать в обучение! Вы командуете французами — синими. Пройдём основы шаг за шагом.",
            "Двигайте карту пальцем, приближайте и отдаляйте двумя пальцами.",
            "Тапните по своему отряду — синей линии солдат. Внизу появятся его приказы.",
            "Проведите пальцем от отряда туда, куда ему идти, — он пойдёт. Можно и дважды тапнуть по земле. В дальний путь отряд сам перестроится в колонну и пойдёт по дорогам.",
            "Потяните за кольцо вокруг выделенного отряда — он развернётся на месте. Линия стреляет только вперёд, в жёлтый сектор.",
            "Кнопка «Марш» перестраивает отряд в колонну: она быстрее ходит, но почти не стреляет. «В линию» — обратно.",
            "Деревни дают золото. Приведите отряд в деревню и постойте в ней — флаг станет вашим.",
            "Австрийцы выступили! Встретьте их линией. Когда враг войдёт в жёлтый сектор, нажмите «Залп!». Чем ближе враг, тем больше попаданий.",
            "После залпа отряд перезаряжается — кольцо за отрядом заполняется. Полоска над отрядом — боевой дух: потери его снижают, а отряд с низким духом бежит.",
            "Добейте врага в штыки: «В штыки», затем тап по врагу. Удар во фланг или в тыл страшнее, чем в лоб.",
            "Пора нанимать войска. Тапните по своему штабу — большому зданию слева — и постройте казарму.",
            "Когда казарма достроится, тапните по ней и включите найм фузилёров или гренадёров.",
            "Рекруты собираются у казармы. Когда их наберётся на отряд, нажмите его кружок слева вверху — отряд встанет в строй.",
            "Цель боя — захватить все деревни или разрушить вражеский штаб: выделите отряд и тапните по вражескому зданию. Дальше — сами. Удачи!"
        };

        [Tooltip("На сколько нужно сдвинуть карту (u) или изменить приближение, чтобы урок камеры засчитался.")]
        [SerializeField] private float cameraMoveToPass = 4f;
        [Tooltip("На сколько нужно уйти отряду (u) или развернуться (градусы), чтобы урок засчитался.")]
        [SerializeField] private float moveToPass = 3f;
        [SerializeField] private float turnToPass = 25f;

        private Match match;
        private Side side;
        private BattleInputController input;
        private Camera view;
        private GatedController enemyTactics;
        private GatedController enemyStrategy;

        private int step = -1;
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

            if (IsDone((Step)step))
            {
                Go(step + 1);
            }
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
            panel.Show(messages[step], WaitsForNext((Step)step), step, messages.Length);
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

            if (current == Step.Volley)
            {
                enemyTactics.Active = true;
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
