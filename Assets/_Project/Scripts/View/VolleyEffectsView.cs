using System.Collections.Generic;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Marchlands.View
{
    /// <summary>
    /// Обратная связь залпа: пороховой дым вдоль фронта стрелка и звук. Павших показывает отряд (<see cref="CasualtiesView"/>).
    /// Случайность здесь чисто визуальная и на симуляцию не влияет, поэтому идёт через UnityEngine.Random.
    /// </summary>
    public sealed class VolleyEffectsView : MonoBehaviour
    {
        [SerializeField] private SquadViewRegistry squadViews;

        [Header("Дым")]
        [SerializeField] private ParticleSystem smoke;
        [Tooltip("За сколько секунд облако рассеивается (smokeDuration из раздела 6).")]
        [SerializeField] private float smokeDuration = 6f;
        [Tooltip("Сколько клубов дыма на одного стрелка.")]
        [SerializeField] private float puffsPerShooter = 0.6f;
        [SerializeField] private Vector2 puffSize = new Vector2(1.1f, 1.8f);
        [Tooltip("Скорость, с которой дым выносит вперёд от фронта, u/s.")]
        [SerializeField] private Vector2 puffSpeed = new Vector2(0.15f, 0.5f);
        [SerializeField] private Color smokeColor = new Color(0.9f, 0.9f, 0.88f, 0.75f);
        [Tooltip("На каком расстоянии от центра здания в сторону цели стреляют часовые, u.")]
        [SerializeField] private float sentryFireOffset = 2.5f;
        [Tooltip("Сколько клубов дыма даёт залп часовых, если их фигурок не видно.")]
        [SerializeField] private int sentryPuffs = 3;
        [Tooltip("Блокгауз: на каком расстоянии от центра — бойницы стены, обращённой к цели, и на какую ширину стены они разнесены, u.")]
        [SerializeField] private float blockhouseWallOffset = 1.7f;
        [SerializeField] private float blockhouseWallSpread = 1.2f;
        [Tooltip("Насколько впереди стрелка-одиночки (часовой, рекрут) встаёт клуб дыма, u.")]
        [SerializeField] private float muzzleOffset = 0.45f;
        [Tooltip("Размер клуба дыма одиночного выстрела относительно залпового.")]
        [SerializeField] private float singlePuffScale = 0.75f;

        [Header("Звук")]
        [SerializeField] private AudioClip volleyClip;
        [SerializeField] private int audioVoices = 4;
        [SerializeField] private Vector2 volumeRange = new Vector2(0.35f, 1f);
        [SerializeField] private Vector2 pitchRange = new Vector2(0.9f, 1.08f);
        [Tooltip("Насколько сильно звук смещается в стерео по положению на экране.")]
        [SerializeField] private float stereoWidth = 0.6f;

        [Header("Дождь: сырой порох")]
        [Tooltip("Залп в дождь звучит глуше: срез высоких частот, Гц (в сухую погоду фильтр не мешает).")]
        [SerializeField] private float rainLowPass = 2200f;
        [Tooltip("И ниже по тону.")]
        [SerializeField] private float rainPitch = 0.88f;
        [Tooltip("Щелчков осечек на залп — не больше.")]
        [SerializeField] private int maxMisfireClicks = 4;
        [SerializeField] private float misfireVolume = 0.5f;
        [Tooltip("Осечка — только вспышка на полке: крошечный клуб дыма, доля залпового.")]
        [SerializeField] private float misfirePuffScale = 0.3f;

        private readonly List<AudioSource> voices = new List<AudioSource>();
        private readonly List<AudioLowPassFilter> filters = new List<AudioLowPassFilter>();
        private readonly List<AudioSource> clickVoices = new List<AudioSource>();
        private AudioClip misfireClip;
        private int nextClick;
        private readonly List<float2> muzzles = new List<float2>();
        private BuildingViewRegistry buildingViews;

        private Match match;
        private int nextVoice;

        /// <param name="buildingViews">Откуда брать стрелков толпы рекрутов и часовых; null — дым по фронту и у стены.</param>
        public void Initialize(Match match, BuildingViewRegistry buildingViews = null)
        {
            this.match = match;
            this.buildingViews = buildingViews;
            // Система без собственной эмиссии: клубы выпускаются только через Emit при залпе.
            smoke.Play();
            // У каждого голоса свой фильтр: в дождь залп глуше.
            for (int i = 0; i < audioVoices; i++)
            {
                var voice = new GameObject($"Volley Voice {i}");
                voice.transform.SetParent(transform, false);
                var source = voice.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                voices.Add(source);
                var filter = voice.AddComponent<AudioLowPassFilter>();
                filter.cutoffFrequency = 22000f;
                filters.Add(filter);
            }

            misfireClip = CreateMisfireClip();
            for (int i = 0; i < maxMisfireClicks; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                clickVoices.Add(source);
            }
        }

        /// <summary>Осечка: сухой щелчок кремня и короткое шипение вспыхнувшего на полке пороха (звук-заглушка).</summary>
        private static AudioClip CreateMisfireClip()
        {
            const int rate = 22050;
            int length = (int)(rate * 0.22f);
            var data = new float[length];
            var random = new System.Random(17);
            float low = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                low += (noise - low) * 0.35f;
                float click = t < 0.006f ? noise * (1f - t / 0.006f) : 0f;
                float hiss = t > 0.02f ? low * math.exp(-(t - 0.02f) * 22f) * 0.6f : 0f;
                data[i] = math.clamp(click + hiss, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Misfire", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Показывает залпы, произошедшие в последнем тике.</summary>
        public void Play(IReadOnlyList<VolleyEvent> volleys)
        {
            foreach (VolleyEvent volley in volleys)
            {
                Squad shooter = match.FindSquad(volley.ShooterId);
                if (shooter == null)
                {
                    continue;
                }

                // Выстрел вразнобой из толпы: клуб дыма у того, кто стрелял, а не по всей толпе.
                if (volley.ShooterIndex >= 0 && buildingViews != null && shooter.IsCamp
                    && buildingViews.TryGetCampShooter(shooter, volley.ShooterIndex, out float2 at))
                {
                    if (volley.Shooters > 0)
                    {
                        EmitSingleShot(at, volley.TargetPosition);
                        PlaySound(at, volley.Shooters, volley.Misfires);
                    }
                    else
                    {
                        EmitMisfire(at, volley.TargetPosition);
                        PlayMisfires(at, volley.Misfires);
                    }

                    continue;
                }

                EmitSmoke(shooter, volley.Shooters, volley.Misfires);
                PlaySound(shooter.Position, volley.Shooters, volley.Misfires);
            }
        }

        /// <summary>Показывает залпы часовых зданий за последний тик: клубы дыма у стены, обращённой к цели, и выстрел.</summary>
        public void PlaySentries(IReadOnlyList<SentryVolley> volleys)
        {
            foreach (SentryVolley volley in volleys)
            {
                // Видны часовые — у каждого свой клуб дыма перед ружьём.
                muzzles.Clear();
                buildingViews?.GetSentryMuzzles(volley.BuildingId, muzzles);
                if (muzzles.Count > 0)
                {
                    // В дождь часть часовых даёт осечку: у них лишь вспышка на полке.
                    for (int i = 0; i < muzzles.Count; i++)
                    {
                        if (i < volley.Shooters)
                        {
                            EmitSingleShot(muzzles[i], volley.TargetPosition);
                        }
                        else
                        {
                            EmitMisfire(muzzles[i], volley.TargetPosition);
                        }
                    }

                    PlaySound(volley.BuildingPosition, volley.Shooters, volley.Misfires);
                    continue;
                }

                float2 forward = math.normalizesafe(volley.TargetPosition - volley.BuildingPosition, new float2(1f, 0f));
                float2 right = new float2(forward.y, -forward.x);
                Building building = match.FindBuilding(volley.BuildingId);
                if (building != null && building.Type == BuildingType.Blockhouse)
                {
                    // Из каждой бойницы стены, обращённой к цели, — свой клуб дыма; стрелков не видно.
                    float2 wall = volley.BuildingPosition + forward * blockhouseWallOffset;
                    int loopholes = volley.Shooters + volley.Misfires;
                    for (int i = 0; i < loopholes; i++)
                    {
                        float along = loopholes > 1 ? math.lerp(-blockhouseWallSpread, blockhouseWallSpread, i / (loopholes - 1f)) : 0f;
                        float2 loophole = wall + right * (along + Random.Range(-0.1f, 0.1f));
                        // Бойницы с осечкой — через одну, а не подряд с краю.
                        bool fired = (i * 7 % math.max(1, loopholes)) < volley.Shooters;
                        if (fired)
                        {
                            EmitSingleShot(loophole, volley.TargetPosition);
                        }
                        else
                        {
                            EmitMisfire(loophole, volley.TargetPosition);
                        }
                    }

                    PlaySound(volley.BuildingPosition, volley.Shooters, volley.Misfires);
                    continue;
                }

                float2 origin = volley.BuildingPosition + forward * sentryFireOffset;
                for (int i = 0; i < sentryPuffs; i++)
                {
                    float2 position = origin + right * Random.Range(-0.8f, 0.8f) + forward * Random.Range(0.1f, 0.4f);
                    EmitPuff(position, forward * Random.Range(puffSpeed.x, puffSpeed.y));
                }

                PlaySound(volley.BuildingPosition, volley.Shooters, volley.Misfires);
            }
        }

        /// <summary>
        /// Облако вдоль фронта. В дождь дымят только выстрелившие: места фронта с осечкой — с прорехами,
        /// там лишь крошечные вспышки на полках.
        /// </summary>
        private void EmitSmoke(Squad shooter, int fired, int misfires)
        {
            float2 front = FireGeometry.FrontCenter(shooter, match.Settings.squads);
            float halfWidth = FireGeometry.FrontHalfWidth(shooter, match.Settings.squads);
            float2 forward = Angles.Direction(shooter.Facing);
            float2 right = Angles.Right(shooter.Facing);
            int total = fired + misfires;
            int slots = math.max(3, (int)math.ceil(total * puffsPerShooter));
            float firedShare = total > 0 ? (float)fired / total : 1f;

            for (int i = 0; i < slots; i++)
            {
                float along = math.lerp(-halfWidth, halfWidth, (i + Random.value) / slots);
                float2 position = front + right * along + forward * Random.Range(0.1f, 0.6f);
                float2 velocity = forward * Random.Range(puffSpeed.x, puffSpeed.y) + right * Random.Range(-0.1f, 0.1f);
                if (Random.value < firedShare)
                {
                    EmitPuff(position, velocity);
                }
                else
                {
                    EmitPuff(front + right * along + forward * 0.15f, velocity * 0.3f, misfirePuffScale);
                }
            }
        }

        /// <summary>Осечка: только вспышка на полке — крошечный клуб у стрелка.</summary>
        private void EmitMisfire(float2 shooter, float2 target)
        {
            float2 forward = math.normalizesafe(target - shooter, new float2(1f, 0f));
            EmitPuff(shooter + forward * (muzzleOffset * 0.5f), forward * puffSpeed.x * 0.3f, misfirePuffScale);
        }

        /// <summary>Один выстрел: клуб дыма перед стрелком в сторону цели.</summary>
        private void EmitSingleShot(float2 shooter, float2 target)
        {
            float2 forward = math.normalizesafe(target - shooter, new float2(1f, 0f));
            float2 position = shooter + forward * (muzzleOffset + Random.Range(0f, 0.15f));
            EmitPuff(position, forward * Random.Range(puffSpeed.x, puffSpeed.y), singlePuffScale);
        }

        private void PlayMisfires(float2 at, int misfires)
        {
            if (misfires <= 0 || misfireClip == null || clickVoices.Count == 0)
            {
                return;
            }

            float pan = PanOf(at);
            int clicks = math.min(misfires, clickVoices.Count);
            for (int i = 0; i < clicks; i++)
            {
                AudioSource source = clickVoices[nextClick];
                nextClick = (nextClick + 1) % clickVoices.Count;
                source.clip = misfireClip;
                source.volume = misfireVolume * Random.Range(0.6f, 1f);
                source.pitch = Random.Range(0.85f, 1.2f);
                source.panStereo = pan;
                source.PlayDelayed(Random.Range(0f, 0.25f));
            }
        }

        [Header("Пушки")]
        [Tooltip("Ядро летит до падения столько секунд на каждые 10 u дистанции.")]
        [SerializeField] private float ballFlightPer10 = 0.55f;
        [Tooltip("Высота навесной траектории — доля дистанции (на экране ядро «поднимается» вверх и растёт).")]
        [SerializeField] private float ballArcHeight = 0.35f;
        [SerializeField] private float ballSize = 0.28f;
        [Tooltip("Скорость ядра после падения, на рикошете, u/s.")]
        [SerializeField] private float ricochetSpeed = 14f;
        [Tooltip("Через сколько u рикошета ядро снова касается земли и поднимает пыль.")]
        [SerializeField] private float bounceStep = 2.5f;
        [SerializeField] private Color dustColor = new Color(0.62f, 0.52f, 0.36f, 0.75f);
        [Tooltip("Тон выстрела пушки относительно залпа — ниже, гулче.")]
        [SerializeField] private float cannonPitch = 0.5f;
        [SerializeField] private float cannonVolume = 1f;
        [Tooltip("Сколько клубов дыма даёт выстрел пушки.")]
        [SerializeField] private int cannonPuffs = 5;
        [SerializeField] private int ballSortingOrder = 40;
        [Header("Взрыв ядра")]
        [Tooltip("Вспышка там, где ядро ударило в землю: растёт до explosionSize и гаснет за explosionTime.")]
        [SerializeField] private Color explosionColor = new Color(1f, 0.78f, 0.35f, 0.95f);
        [SerializeField] private Color fireColor = new Color(1f, 0.5f, 0.15f, 0.9f);
        [SerializeField] private Color blastSmokeColor = new Color(0.32f, 0.29f, 0.26f, 0.8f);
        [SerializeField] private float explosionSize = 2.6f;
        [SerializeField] private float explosionTime = 0.35f;
        [SerializeField] private float explosionVolume = 0.7f;
        [SerializeField] private float explosionPitch = 0.33f;

        private sealed class Ball
        {
            public SpriteRenderer Renderer;
            public SpriteRenderer Shadow;
            public float2 From;
            public float2 Impact;
            public float2 End;
            public float Flight;
            public float Age;
            public float NextBounce;
        }

        private readonly List<Ball> balls = new List<Ball>();

        private sealed class Blast
        {
            public SpriteRenderer Renderer;
            public float Age;
        }

        private readonly List<Blast> blasts = new List<Blast>();
        private Sprite ballSprite;

        /// <summary>Выстрелы пушек за тик: дым у ствола, ядро по дуге с рикошетом или веер картечи, гулкий звук.</summary>
        public void PlayArtillery(IReadOnlyList<ArtilleryShot> shots)
        {
            foreach (ArtilleryShot shot in shots)
            {
                float2 direction = math.normalizesafe(shot.Impact - shot.Muzzle, new float2(0f, 1f));
                for (int i = 0; i < cannonPuffs; i++)
                {
                    float2 spread = new float2(Random.Range(-0.4f, 0.4f), Random.Range(-0.4f, 0.4f));
                    EmitPuff(shot.Muzzle + direction * Random.Range(0.3f, 1.4f) + spread, direction * Random.Range(puffSpeed.x, puffSpeed.y) * 2f, 1.3f);
                }

                if (shot.Mode == ShotMode.Canister)
                {
                    // Веер картечи: пыль там, куда легли пули.
                    float2 side = new float2(direction.y, -direction.x);
                    float length = math.distance(shot.Muzzle, shot.Impact);
                    for (int i = 0; i < 10; i++)
                    {
                        float along = Random.Range(0.5f, 1.1f) * length;
                        float across = Random.Range(-0.35f, 0.35f) * along;
                        EmitDust(shot.Muzzle + direction * along + side * across, 0.35f);
                    }
                }
                else
                {
                    LaunchBall(shot);
                }

                PlayCannon(shot.Muzzle);
            }
        }

        private void LaunchBall(ArtilleryShot shot)
        {
            if (ballSprite == null)
            {
                ballSprite = CreateBallSprite();
            }

            var ball = new Ball
            {
                From = shot.Muzzle,
                Impact = shot.Impact,
                End = shot.RicochetEnd,
                Flight = math.max(0.2f, math.distance(shot.Muzzle, shot.Impact) / 10f * ballFlightPer10),
                Renderer = CreateBallRenderer("Ball", Color.black * 0.85f + Color.white * 0.1f, ballSortingOrder),
                Shadow = CreateBallRenderer("Ball Shadow", new Color(0f, 0f, 0f, 0.3f), ballSortingOrder - 1)
            };
            ball.NextBounce = bounceStep;
            balls.Add(ball);
        }

        private SpriteRenderer CreateBallRenderer(string name, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = ballSprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            go.transform.localScale = Vector3.one * ballSize;
            return renderer;
        }

        private static Sprite CreateBallSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = math.length(new float2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, math.saturate(size * 0.5f - d)));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private void Update()
        {
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                Ball ball = balls[i];
                ball.Age += Time.deltaTime;
                float2 ground;
                float height;
                if (ball.Age < ball.Flight)
                {
                    // Навесная дуга: по земле — равномерно, высота — парабола; высоту показываем сдвигом вверх и размером.
                    float t = ball.Age / ball.Flight;
                    ground = math.lerp(ball.From, ball.Impact, t);
                    height = 4f * t * (1f - t) * ballArcHeight * math.distance(ball.From, ball.Impact);
                    if (t + Time.deltaTime / ball.Flight >= 1f)
                    {
                        Explode(ball.Impact);
                    }
                }
                else
                {
                    float rolled = (ball.Age - ball.Flight) * ricochetSpeed;
                    float length = math.distance(ball.Impact, ball.End);
                    if (rolled >= length)
                    {
                        Destroy(ball.Renderer.gameObject);
                        Destroy(ball.Shadow.gameObject);
                        balls.RemoveAt(i);
                        continue;
                    }

                    ground = math.lerp(ball.Impact, ball.End, length > 0f ? rolled / length : 1f);
                    // Рикошет: низкие подскоки, на каждом касании — облачко пыли.
                    float phase = rolled / bounceStep;
                    height = math.abs(math.sin(phase * math.PI)) * 0.4f * (1f - rolled / math.max(length, 0.01f));
                    if (rolled >= ball.NextBounce)
                    {
                        ball.NextBounce += bounceStep;
                        EmitDust(ground, 0.6f);
                    }
                }

                ball.Shadow.transform.position = new Vector3(ground.x, ground.y, 0f);
                ball.Renderer.transform.position = new Vector3(ground.x, ground.y + height, 0f);
                ball.Renderer.transform.localScale = Vector3.one * ballSize * (1f + height * 0.12f);
            }

            UpdateBlasts();
        }

        /// <summary>
        /// Ядро ударило в землю: вспышка, выброс огня и тёмного дыма, комья земли и глухой удар.
        /// </summary>
        private void Explode(float2 at)
        {
            var blast = new Blast { Renderer = CreateBallRenderer("Blast", explosionColor, ballSortingOrder + 1) };
            blast.Renderer.transform.position = new Vector3(at.x, at.y, 0f);
            blasts.Add(blast);

            for (int i = 0; i < 8; i++)
            {
                float2 direction = Angles.Direction(Random.Range(0f, 360f));
                EmitParticle(at + direction * 0.2f, direction * Random.Range(1.5f, 3.5f), fireColor, Random.Range(0.4f, 0.8f), smokeDuration * 0.12f);
            }

            for (int i = 0; i < 6; i++)
            {
                float2 direction = Angles.Direction(Random.Range(0f, 360f));
                EmitParticle(at + direction * 0.4f, direction * Random.Range(0.4f, 1.2f) + new float2(0f, 0.4f), blastSmokeColor,
                    Random.Range(puffSize.x, puffSize.y) * 1.1f, smokeDuration * 0.5f);
            }

            EmitDust(at, 1.8f);
            PlayExplosion(at);
        }

        private void EmitParticle(float2 position, float2 velocity, Color color, float size, float lifetime)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = new Vector3(position.x, position.y, 0f),
                velocity = new Vector3(velocity.x, velocity.y, 0f),
                startSize = size,
                startLifetime = lifetime,
                startColor = color,
                rotation = Random.Range(0f, 360f),
                applyShapeToPosition = false
            };
            smoke.Emit(emit, 1);
        }

        private void UpdateBlasts()
        {
            for (int i = blasts.Count - 1; i >= 0; i--)
            {
                Blast blast = blasts[i];
                blast.Age += Time.deltaTime;
                float t = explosionTime > 0f ? blast.Age / explosionTime : 1f;
                if (t >= 1f)
                {
                    Destroy(blast.Renderer.gameObject);
                    blasts.RemoveAt(i);
                    continue;
                }

                // Быстро вспыхивает и медленнее гаснет.
                blast.Renderer.transform.localScale = Vector3.one * explosionSize * math.sqrt(t);
                Color color = explosionColor;
                color.a *= 1f - t * t;
                blast.Renderer.color = color;
            }
        }

        private void PlayExplosion(float2 at)
        {
            if (volleyClip == null || voices.Count == 0)
            {
                return;
            }

            int index = nextVoice;
            AudioSource source = voices[index];
            nextVoice = (nextVoice + 1) % voices.Count;
            filters[index].cutoffFrequency = 1500f;
            source.clip = volleyClip;
            source.volume = explosionVolume;
            source.pitch = explosionPitch * Random.Range(0.9f, 1.1f);
            source.panStereo = PanOf(at);
            source.Play();
        }


        private void EmitDust(float2 position, float scale)
        {
            for (int i = 0; i < 3; i++)
            {
                var emit = new ParticleSystem.EmitParams
                {
                    position = new Vector3(position.x + Random.Range(-0.3f, 0.3f), position.y + Random.Range(-0.3f, 0.3f), 0f),
                    velocity = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.1f, 0.5f), 0f),
                    startSize = Random.Range(puffSize.x, puffSize.y) * 0.6f * scale,
                    startLifetime = smokeDuration * 0.35f,
                    startColor = dustColor,
                    rotation = Random.Range(0f, 360f),
                    applyShapeToPosition = false
                };
                smoke.Emit(emit, 1);
            }
        }

        private void PlayCannon(float2 at)
        {
            if (volleyClip == null || voices.Count == 0)
            {
                return;
            }

            int index = nextVoice;
            AudioSource source = voices[index];
            nextVoice = (nextVoice + 1) % voices.Count;
            filters[index].cutoffFrequency = 22000f;
            source.clip = volleyClip;
            source.volume = cannonVolume;
            source.pitch = cannonPitch * Random.Range(0.92f, 1.06f);
            source.panStereo = PanOf(at);
            source.Play();
        }

        private void EmitPuff(float2 position, float2 velocity, float scale = 1f)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = new Vector3(position.x, position.y, 0f),
                velocity = new Vector3(velocity.x, velocity.y, 0f),
                startSize = Random.Range(puffSize.x, puffSize.y) * scale,
                startLifetime = smokeDuration * Random.Range(0.8f, 1f),
                startColor = smokeColor,
                rotation = Random.Range(0f, 360f),
                applyShapeToPosition = false
            };
            smoke.Emit(emit, 1);
        }

        private float PanOf(float2 at)
        {
            Camera view = Camera.main;
            if (view == null)
            {
                return 0f;
            }

            Vector3 viewport = view.WorldToViewportPoint(new Vector3(at.x, at.y, 0f));
            return math.clamp((viewport.x - 0.5f) * 2f, -1f, 1f) * stereoWidth;
        }

        /// <summary>
        /// Залп: громкость по числу выстреливших. В дождь — глуше и ниже (сырой порох горит вяло),
        /// а осечки слышны отдельными щелчками.
        /// </summary>
        private void PlaySound(float2 at, int shooters, int misfires = 0)
        {
            PlayMisfires(at, misfires);
            if (volleyClip == null || voices.Count == 0 || shooters <= 0)
            {
                return;
            }

            int index = nextVoice;
            AudioSource source = voices[index];
            nextVoice = (nextVoice + 1) % voices.Count;
            bool raining = match.IsRaining;
            filters[index].cutoffFrequency = raining ? rainLowPass : 22000f;

            float strength = math.saturate((float)shooters / match.Settings.grenadiers.squadSize);
            source.clip = volleyClip;
            source.volume = math.lerp(volumeRange.x, volumeRange.y, strength);
            source.pitch = Random.Range(pitchRange.x, pitchRange.y) * (raining ? rainPitch : 1f);
            source.panStereo = PanOf(at);
            source.Play();
        }
    }
}
