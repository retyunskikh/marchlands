using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// Общая громкость игры (все звуки и музыка, через AudioListener.volume), выбранная игроком: 0–1, по умолчанию
    /// 100%; хранится в PlayerPrefs и применяется при запуске. Громкость музыки задаётся отдельно, в доле от общей.
    /// </summary>
    public static class GameVolume
    {
        private const string VolumeKey = "game.volume";
        private static float? volume;

        /// <summary>Громкость по умолчанию, пока игрок её не менял.</summary>
        public const float DefaultVolume = 1f;

        public static float Volume
        {
            get
            {
                volume ??= Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, DefaultVolume));
                return volume.Value;
            }

            set
            {
                volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, volume.Value);
                PlayerPrefs.Save();
                Apply();
            }
        }

        public static void Apply()
        {
            AudioListener.volume = Volume;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyOnStart()
        {
            Apply();
        }
    }
}
