using System.Collections.Generic;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>Звуки-заглушки боя кроме залпа: лязг, пока идёт рукопашная, и колокол при захвате деревни.</summary>
    public sealed class BattleSoundsView : MonoBehaviour
    {
        [SerializeField] private AudioClip meleeClip;
        [SerializeField] private AudioClip captureClip;
        [SerializeField] private float meleeVolume = 0.6f;
        [Tooltip("Как быстро лязг нарастает и стихает, громкости в секунду.")]
        [SerializeField] private float meleeFadeSpeed = 2f;
        [SerializeField] private float captureVolume = 0.8f;

        private Match match;
        private AudioSource meleeSource;
        private AudioSource captureSource;

        public void Initialize(Match match)
        {
            this.match = match;
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
            if (captures.Count > 0 && captureClip != null)
            {
                captureSource.PlayOneShot(captureClip, captureVolume);
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

            // На паузе и после конца матча лязг не звучит.
            meleeSource.pitch = Time.timeScale > 0f && match.Result == null ? 1f : 0f;
        }
    }
}
