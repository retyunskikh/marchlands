using System.Collections.Generic;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using UnityEngine;
using UnityEngine.Serialization;

namespace Marchlands.View
{
    /// <summary>
    /// Звуки-заглушки боя кроме залпа: шум сражения, пока идёт рукопашная; горн, когда деревню взял игрок,
    /// и колокол-предупреждение, когда её взял противник.
    /// </summary>
    public sealed class BattleSoundsView : MonoBehaviour
    {
        [SerializeField] private AudioClip meleeClip;
        [Tooltip("Деревню взял игрок.")]
        [SerializeField] private AudioClip ownCaptureClip;
        [Tooltip("Деревню взял противник.")]
        [FormerlySerializedAs("captureClip")]
        [SerializeField] private AudioClip enemyCaptureClip;
        [SerializeField] private float meleeVolume = 0.6f;
        [Tooltip("Как быстро шум сражения нарастает и стихает, громкости в секунду.")]
        [SerializeField] private float meleeFadeSpeed = 2f;
        [SerializeField] private float captureVolume = 0.8f;
        [Tooltip("Горн нашего захвата тише колокола вражеского.")]
        [SerializeField] private float ownCaptureVolume = 0.45f;

        private Match match;
        private Side playerSide;
        private AudioSource meleeSource;
        private AudioSource captureSource;

        public void Initialize(Match match, Side playerSide)
        {
            this.match = match;
            this.playerSide = playerSide;
            meleeSource = gameObject.AddComponent<AudioSource>();
            meleeSource.clip = meleeClip;
            meleeSource.loop = true;
            meleeSource.playOnAwake = false;
            meleeSource.volume = 0f;
            captureSource = gameObject.AddComponent<AudioSource>();
            captureSource.playOnAwake = false;
        }

        /// <summary>Деревни, сменившие владельца в последнем тике.</summary>
        public void Play(IReadOnlyList<Village> captures)
        {
            foreach (Village village in captures)
            {
                AudioClip clip = village.Owner == playerSide ? ownCaptureClip : enemyCaptureClip;
                if (clip != null)
                {
                    captureSource.PlayOneShot(clip, village.Owner == playerSide ? ownCaptureVolume : captureVolume);
                }
            }
        }

        private void Update()
        {
            if (match == null || meleeClip == null)
            {
                return;
            }

            float target = match.Engagements.Count > 0 ? meleeVolume : 0f;
            meleeSource.volume = Mathf.MoveTowards(meleeSource.volume, target, meleeFadeSpeed * Time.unscaledDeltaTime);
            if (meleeSource.volume > 0f && !meleeSource.isPlaying)
            {
                meleeSource.Play();
            }
            else if (meleeSource.volume <= 0f && meleeSource.isPlaying)
            {
                meleeSource.Stop();
            }

            // На паузе и после конца матча шум сражения не звучит.
            meleeSource.pitch = Time.timeScale > 0f && match.Result == null ? 1f : 0f;
        }
    }
}
