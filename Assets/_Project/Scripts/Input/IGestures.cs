using System;
using UnityEngine;

namespace Marchlands.Input
{
    /// <summary>Источник жестов боя: пальцы (<see cref="TouchGestures"/>) или левая кнопка мыши (<see cref="MouseGestures"/>).</summary>
    public interface IGestures
    {
        /// <summary>Тап (клик): позиция на экране.</summary>
        event Action<Vector2> Tapped;

        /// <summary>Начало протяжки: точка нажатия.</summary>
        event Action<Vector2> DragStarted;

        /// <summary>Протяжка продолжается: начальная и текущая точки.</summary>
        event Action<Vector2, Vector2> DragUpdated;

        /// <summary>Протяжка закончена: начальная и конечная точки.</summary>
        event Action<Vector2, Vector2> DragEnded;

        /// <summary>Протяжка прервана.</summary>
        event Action DragCancelled;

        /// <summary>Щипок: центр, его смещение за кадр, отношение нового расстояния к прежнему.</summary>
        event Action<Vector2, Vector2, float> Pinched;

        void Update();
    }
}
