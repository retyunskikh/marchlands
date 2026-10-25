using UnityEngine;

namespace Marchlands.Input
{
    /// <summary>Перевод density-independent pixels в пиксели экрана (1 dp = 1 px при 160 dpi, как в Android).</summary>
    public static class ScreenUnits
    {
        private const float BaselineDpi = 160f;

        public static float DpToPixels(float dp)
        {
            // Некоторые устройства и редактор не сообщают dpi — тогда считаем экран базовым.
            float dpi = Screen.dpi > 0f ? Screen.dpi : BaselineDpi;
            return dp * dpi / BaselineDpi;
        }
    }
}
