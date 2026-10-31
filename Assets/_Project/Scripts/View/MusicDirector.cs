using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Marchlands.View
{
    /// <summary>
    /// Фоновая музыка: спокойный плейлист и марши. Композиция звучит не дольше maxPlayTime, затихает, и наступает
    /// тишина на случайное время из silenceTime; затем — следующая композиция того же плейлиста (по кругу).
    /// Марш вступает сразу, когда не бегущий враг подходит к не бегущему отряду игрока ближе tensionRadius;
    /// если угрозы нет calmDelay секунд, марш затихает, а после паузы играет спокойная музыка.
    /// Без матча (главное меню) играет только спокойная музыка. На паузе игры музыка за pauseFadeTime затихает
    /// и встаёт на паузу, после паузы продолжается с того же места.
    /// </summary>
    public sealed class MusicDirector : MonoBehaviour
    {
        private enum Mood
        {
            Calm,
            March
        }

        [SerializeField] private AudioClip[] calmTracks;
        [SerializeField] private AudioClip[] marchTracks;
        [SerializeField] private float volume = 0.45f;
        [Tooltip("Длительность нарастания и затухания, s.")]
        [SerializeField] private float crossfadeTime = 3f;
        [Tooltip("Сколько максимум звучит одна композиция, s.")]
        [SerializeField] private float maxPlayTime = 90f;
        [Tooltip("Тишина между композициями, s: от и до.")]
        [SerializeField] private Vector2 silenceTime = new Vector2(15f, 40f);
        [Tooltip("Враг ближе этого к отряду игрока — пора играть марш, u.")]
        [SerializeField] private float tensionRadius = 25f;
        [Tooltip("Сколько секунд без угрозы нужно, чтобы марш затих.")]
        [SerializeField] private float calmDelay = 20f;
        [Tooltip("Как часто проверять обстановку, s.")]
        [SerializeField] private float checkInterval = 0.5f;
        [Tooltip("За сколько секунд музыка затихает на паузе игры и возвращается после неё.")]
        [SerializeField] private float pauseFadeTime = 1f;

        private readonly AudioSource[] sources = new AudioSource[2];
        private readonly float[] targetVolumes = new float[2];
        private readonly float[] volumes = new float[2];
        private readonly int[] nextTrack = new int[2];

        private Match match;
        private Side playerSide;
        private Mood mood = Mood.Calm;
        private int activeSource;
        private bool silent;
        private float playTimer;
        private float silenceTimer;
        private float sinceThreat = float.MaxValue;
        private float sinceCheck;
        private bool paused;
        private float pauseLevel = 1f;

        private const string EnabledKey = "music.enabled";
        private static bool? musicEnabled;

        /// <summary>Включена ли музыка; выбор игрока хранится в PlayerPrefs.</summary>
        public static bool Enabled
        {
            get
            {
                musicEnabled ??= PlayerPrefs.GetInt(EnabledKey, 1) == 1;
                return musicEnabled.Value;
            }
            set
            {
                musicEnabled = value;
                PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>В бою: следить за обстановкой. Без вызова (меню) — только спокойная музыка.</summary>
        public void Initialize(Match match, Side playerSide)
        {
            this.match = match;
            this.playerSide = playerSide;
        }

        private void Awake()
        {
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].loop = false;
                sources[i].spatialBlend = 0f;
                sources[i].ignoreListenerPause = true;
            }

            nextTrack[(int)Mood.Calm] = RandomStart(calmTracks);
            nextTrack[(int)Mood.March] = RandomStart(marchTracks);
            PlayNext(Mood.Calm, instant: true);
        }

        /// <summary>Пауза игры: музыка плавно затихает и останавливается; после паузы продолжается с того же места.</summary>
        public void SetPaused(bool value)
        {
            if (paused == value)
            {
                return;
            }

            paused = value;
            if (!paused)
            {
                foreach (AudioSource source in sources)
                {
                    source.UnPause();
                }
            }
        }

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            foreach (AudioSource source in sources)
            {
                source.mute = !Enabled;
            }

            if (UpdatePause(delta))
            {
                return;
            }

            UpdateMood(delta);

            if (silent)
            {
                silenceTimer -= delta;
                if (silenceTimer <= 0f)
                {
                    PlayNext(mood, instant: false);
                }
            }
            else
            {
                playTimer += delta;
                AudioSource current = sources[activeSource];
                bool ended = !current.isPlaying && Application.isFocused;
                if (ended || playTimer >= maxPlayTime - crossfadeTime)
                {
                    FadeOutToSilence();
                }
            }

            UpdateVolumes(delta);
        }

        private void UpdateMood(float delta)
        {
            if (match == null)
            {
                return;
            }

            sinceThreat += delta;
            sinceCheck += delta;
            if (sinceCheck < checkInterval)
            {
                return;
            }

            sinceCheck = 0f;
            if (IsThreatened())
            {
                sinceThreat = 0f;
            }

            Mood wanted = sinceThreat <= calmDelay ? Mood.March : Mood.Calm;
            if (wanted == mood)
            {
                return;
            }

            if (wanted == Mood.March)
            {
                // Бой близко — марш сразу, даже посреди паузы.
                PlayNext(Mood.March, instant: false);
            }
            else
            {
                mood = Mood.Calm;
                FadeOutToSilence();
            }
        }

        private bool IsThreatened()
        {
            float radiusSq = tensionRadius * tensionRadius;
            foreach (Squad own in match.Squads)
            {
                if (own.Side != playerSide || own.IsRouting || own.IsCamp)
                {
                    continue;
                }

                foreach (Squad enemy in match.Squads)
                {
                    if (enemy.Side != playerSide && !enemy.IsRouting && !enemy.IsCamp && math.distancesq(own.Position, enemy.Position) <= radiusSq)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Ведёт громкость паузы к нулю или к полной; совсем затихшую музыку ставит на паузу.
        /// Возвращает true, пока игра на паузе: композиции, тишина и настроение тогда не меняются.
        /// </summary>
        private bool UpdatePause(float delta)
        {
            float step = pauseFadeTime > 0f ? delta / pauseFadeTime : 1f;
            pauseLevel = Mathf.MoveTowards(pauseLevel, paused ? 0f : 1f, step);
            if (!paused)
            {
                return false;
            }

            for (int i = 0; i < sources.Length; i++)
            {
                sources[i].volume = volumes[i] * pauseLevel;
                if (pauseLevel <= 0f && sources[i].isPlaying)
                {
                    sources[i].Pause();
                }
            }

            return true;
        }

        /// <summary>Громкость каждого источника плавно идёт к целевой; затихший источник останавливается.</summary>
        private void UpdateVolumes(float delta)
        {
            float step = crossfadeTime > 0f ? volume * delta / crossfadeTime : volume;
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                volumes[i] = Mathf.MoveTowards(volumes[i], targetVolumes[i], step);
                source.volume = volumes[i] * pauseLevel;
                if (targetVolumes[i] <= 0f && volumes[i] <= 0f && source.isPlaying)
                {
                    source.Stop();
                }
            }
        }

        private void FadeOutToSilence()
        {
            targetVolumes[activeSource] = 0f;
            silent = true;
            silenceTimer = Random.Range(silenceTime.x, silenceTime.y);
        }

        private void PlayNext(Mood newMood, bool instant)
        {
            AudioClip[] playlist = newMood == Mood.Calm ? calmTracks : marchTracks;
            if (playlist == null || playlist.Length == 0)
            {
                return;
            }

            int index = nextTrack[(int)newMood];
            nextTrack[(int)newMood] = (index + 1) % playlist.Length;
            mood = newMood;

            // Прежняя композиция затихает, новая нарастает на другом источнике.
            targetVolumes[activeSource] = 0f;
            activeSource = 1 - activeSource;
            AudioSource source = sources[activeSource];
            source.clip = playlist[index];
            volumes[activeSource] = instant ? volume : 0f;
            source.volume = volumes[activeSource] * pauseLevel;
            targetVolumes[activeSource] = volume;
            source.Play();
            silent = false;
            playTimer = 0f;
        }

        private static int RandomStart(AudioClip[] playlist)
        {
            return playlist == null || playlist.Length == 0 ? 0 : Random.Range(0, playlist.Length);
        }
    }
}
