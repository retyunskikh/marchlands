using UnityEngine;

namespace Marchlands.UI
{
    /// <summary>Карта, выбранная в главном меню: номер в списке карт сцены боя. Запоминается между запусками.</summary>
    public static class MapChoice
    {
        private const string PrefsKey = "Marchlands.Map";

        public static int Selected
        {
            get
            {
                try
                {
                    return PlayerPrefs.GetInt(PrefsKey, 0);
                }
                catch (UnityException)
                {
                    return 0;
                }
            }

            set
            {
                PlayerPrefs.SetInt(PrefsKey, value);
                PlayerPrefs.Save();
            }
        }
    }
}
