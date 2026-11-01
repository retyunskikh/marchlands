using System;
using System.Collections.Generic;

namespace Marchlands.Network
{
    /// <summary>
    /// Комната глазами экрана: в локальной сети (хост или гость) или на сервере в интернете — экран одинаковый.
    /// Карту, стороны, туман и старт меняет только создатель (<see cref="IsHost"/>).
    /// </summary>
    public interface ILobby
    {
        /// <summary>Состояние комнаты; null — ещё не вошли.</summary>
        LobbyState State { get; }

        bool IsHost { get; }

        bool HasPassword { get; }

        IReadOnlyList<ChatLine> Chat { get; }

        bool CanStart { get; }

        event Action Changed;

        event Action<MatchSetup> Started;

        void SetReady(bool ready);

        void SendChat(string text);

        void SetMap(int map);

        void SwapSides();

        void SetFogOfWar(bool fog);

        /// <summary>Начать бой (только создатель, когда оба готовы).</summary>
        void StartMatch();

        void Leave();
    }
}
