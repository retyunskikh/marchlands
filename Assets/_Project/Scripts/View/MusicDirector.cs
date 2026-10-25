using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Marchlands.View
{
    /// <summary>
    /// Фоновая музыка: спокойный плейлист и марши. Марш включается, когда не бегущий враг подходит к любому
    /// не бегущему отряду игрока ближе tensionRadius; спокойная музыка возвращается, если такой угрозы нет
    /// calmDelay секунд. Смена — перекрёстным затуханием; внутри плейлиста треки идут по кругу.
    /// Без матча (главное меню) играет спокойная музыка.
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
        [Tooltip("Длительность перекрёстного затухания, s.")]
        [SerializeField] private float crossfadeTime = 3f;
        [Tooltip("Враг ближе этого к отряду игрока — пора играть марш, u.")]
        [SerializeField] private float tensionRadius = 25f;
        [Tooltip("Сколько секунд без угрозы нужно, чтобы вернулась спокойная музыка.")]
        [SerializeField] private float calmDelay = 20f;
        [Tooltip("Как часто проверять обстановку, s.")]
        [SerializeField] private float checkInterval = 0.5f;

        private readonly AudioSource[] sources = new AudioSource[2];
        private readonly int[] nextTrack = new int[2];

        private Match match;
        private Side playerSide;
        private Mood mood = Mood.Calm;
        private int activeSource;
        private float fade = 1f;
        private float sinceThreat = float.MaxValue;
        private float sinceCheck;

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

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            UpdateMood(delta);

            // Перекрёстное затухание: активный источник нарастает, другой стихает.
            fade = crossfadeTime > 0f ? math.saturate(fade + delta / crossfadeTime) : 1f;
            sources[activeSource].volume = volume * fade;
            AudioSource previous = sources[1 - activeSource];
            previous.volume = volume * (1f - fade);
            if (fade >= 1f && previous.isPlaying)
            {
                previous.Stop();
            }

            AudioSource current = sources[activeSource];
            if (!current.isPlaying && current.clip != null && Application.isFocused)
            {
                // Трек кончился — следующий из того же плейлиста.
                PlayNext(mood, instant: false);
            }
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
            if (wanted != mood)
            {
                PlayNext(wanted, instant: false);
            }
        }

        private bool IsThreatened()
        {
            float radiusSq = tensionRadius * tensionRadius;
            foreach (Squad own in match.Squads)
            {
                if (own.Side != playerSide || own.IsRouting)
                {
                    continue;
                }

                foreach (Squad enemy in match.Squads)
                {
                    if (enemy.Side != playerSide && !enemy.IsRouting && math.distancesq(own.Position, enemy.Position) <= radiusSq)
                    {
                        return true;
                    }
                }
            }

            return false;
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

            activeSource = 1 - activeSource;
            AudioSource source = sources[activeSource];
            source.clip = playlist[index];
            source.volume = instant ? volume : 0f;
            source.Play();
            fade = instant ? 1f : 0f;
        }

        private static int RandomStart(AudioClip[] playlist)
        {
            return playlist == null || playlist.Length == 0 ? 0 : Random.Range(0, playlist.Length);
        }
    }
}
