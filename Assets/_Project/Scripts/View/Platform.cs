using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// На чём идёт игра: ПК (и редактор) — мышь и клавиатура, интерфейс компактнее; телефон — пальцы, кнопки крупные.
    /// Один проект: платформа определяется при запуске, а не отдельной сборкой кода.
    /// </summary>
    public static class Platform
    {
        public static bool IsDesktop
        {
            get
            {
#if UNITY_EDITOR
                return true;
#else
                return !Application.isMobilePlatform;
#endif
            }
        }
    }
}
