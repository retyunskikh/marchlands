using System;
using System.Collections.Generic;
using Marchlands.Simulation.Core;

namespace Marchlands.Network
{
    /// <summary>Строка мини-чата лобби.</summary>
    public readonly struct ChatLine
    {
        public ChatLine(string sender, string text)
        {
            Sender = sender;
            Text = text;
        }

        public string Sender { get; }

        public string Text { get; }
    }

    /// <summary>Куда отправлять сообщения лобби: соединение по номеру.</summary>
    public interface ILobbyTransport
    {
        void Send(int connection, byte[] message);

        void Disconnect(int connection);
    }

    /// <summary>
    /// Комната на стороне создавшего игру (хоста): принимает одного гостя (проверяет версию и пароль), хранит карту,
    /// стороны, туман и готовность, пересылает чат. Любое изменение карты, сторон или тумана снимает готовность.
    /// Начать можно, когда гость есть и оба готовы.
    /// </summary>
    public sealed class LobbyHost : ILobby
    {
        public const int MaxChatLines = 50;

        private readonly ILobbyTransport transport;
        private readonly string passwordHash;
        private readonly List<ChatLine> chat = new List<ChatLine>();
        private int guestConnection = -1;

        public LobbyHost(string serverName, string password, string hostName, ILobbyTransport transport)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            passwordHash = LobbyProtocol.HashPassword(password);
            State = new LobbyState { ServerName = serverName, HostName = hostName };
        }

        public LobbyState State { get; }

        public bool HasPassword => !string.IsNullOrEmpty(passwordHash);

        public IReadOnlyList<ChatLine> Chat => chat;

        public bool IsStarted { get; private set; }

        public int GuestConnection => guestConnection;

        public bool CanStart => State.HasGuest && State.HostReady && State.GuestReady && !IsStarted;

        public bool IsHost => true;

        /// <summary>Что-то в комнате изменилось — перерисовать.</summary>
        public event Action Changed;

        public event Action<MatchSetup> Started;

        /// <summary>Начать бой со случайным seed; параметры — в <see cref="Started"/>.</summary>
        public void StartMatch()
        {
            MatchSetup setup = Start(unchecked((ulong)DateTime.Now.Ticks));
            if (setup != null)
            {
                Started?.Invoke(setup);
            }
        }

        /// <summary>Уход создателя: соединение закрывает меню (гость увидит разрыв).</summary>
        public void Leave()
        {
        }

        public void Receive(int connection, byte[] message)
        {
            NetMessageType type = LobbyProtocol.TypeOf(message);
            if (type == NetMessageType.Hello)
            {
                OnHello(connection, message);
                return;
            }

            if (connection != guestConnection)
            {
                return;
            }

            switch (type)
            {
                case NetMessageType.Ready:
                    State.GuestReady = LobbyProtocol.ReadReady(message);
                    Broadcast();
                    break;
                case NetMessageType.SetNation:
                    State.GuestNation = LobbyProtocol.ReadSetNation(message);
                    ChangeSettings();
                    break;
                case NetMessageType.Chat:
                    LobbyProtocol.ReadChat(message, out _, out string text);
                    Say(State.GuestName, text);
                    break;
                case NetMessageType.Leave:
                    OnDisconnected(connection);
                    break;
            }
        }

        public void OnDisconnected(int connection)
        {
            if (connection != guestConnection)
            {
                return;
            }

            guestConnection = -1;
            State.GuestName = string.Empty;
            State.GuestReady = false;
            State.HostReady = false;
            Changed?.Invoke();
        }

        public void SetMap(int map)
        {
            if (State.Map == map)
            {
                return;
            }

            State.Map = map;
            ChangeSettings();
        }

        public void SwapSides()
        {
            State.HostSide = State.HostSide == Side.France ? Side.Austria : Side.France;
            ChangeSettings();
        }

        /// <summary>Нация создателя.</summary>
        public void SetNation(Nation nation)
        {
            if (State.HostNation == nation)
            {
                return;
            }

            State.HostNation = nation;
            ChangeSettings();
        }

        public void SetFogOfWar(bool fog)
        {
            if (State.FogOfWar == fog)
            {
                return;
            }

            State.FogOfWar = fog;
            ChangeSettings();
        }

        public void SetReady(bool ready)
        {
            State.HostReady = ready;
            Broadcast();
        }

        public void SendChat(string text)
        {
            Say(State.HostName, text);
        }

        /// <summary>Начинает матч: рассылает гостю его параметры и возвращает свои. null — начать нельзя.</summary>
        public MatchSetup Start(ulong seed)
        {
            if (!CanStart)
            {
                return null;
            }

            IsStarted = true;
            var setup = new MatchSetup
            {
                Seed = seed,
                Map = State.Map,
                HostSide = State.HostSide,
                FogOfWar = State.FogOfWar,
                HostNation = State.HostNation,
                GuestNation = State.GuestNation,
                IsHost = true
            };
            transport.Send(guestConnection, LobbyProtocol.StartMatch(setup));
            return setup;
        }

        private void OnHello(int connection, byte[] message)
        {
            LobbyProtocol.ReadHello(message, out int version, out string name, out string hash);
            RejectReason? reason = version != LobbyProtocol.Version ? RejectReason.VersionMismatch
                : IsStarted ? RejectReason.InGame
                : guestConnection >= 0 && guestConnection != connection ? RejectReason.Full
                : hash != passwordHash ? RejectReason.WrongPassword
                : (RejectReason?)null;
            if (reason.HasValue)
            {
                transport.Send(connection, LobbyProtocol.Reject(reason.Value));
                transport.Disconnect(connection);
                return;
            }

            guestConnection = connection;
            State.GuestName = string.IsNullOrWhiteSpace(name) ? "Гость" : name.Trim();
            State.GuestNation = Nation.France;
            State.GuestReady = false;
            State.HostReady = false;
            transport.Send(connection, LobbyProtocol.Welcome());
            foreach (ChatLine line in chat)
            {
                transport.Send(connection, LobbyProtocol.Chat(line.Sender, line.Text));
            }

            Broadcast();
        }

        private void ChangeSettings()
        {
            State.HostReady = false;
            State.GuestReady = false;
            Broadcast();
        }

        private void Say(string sender, string text)
        {
            text = text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var line = new ChatLine(sender, text);
            chat.Add(line);
            if (chat.Count > MaxChatLines)
            {
                chat.RemoveAt(0);
            }

            if (guestConnection >= 0)
            {
                transport.Send(guestConnection, LobbyProtocol.Chat(line.Sender, line.Text));
            }

            Changed?.Invoke();
        }

        private void Broadcast()
        {
            if (guestConnection >= 0)
            {
                transport.Send(guestConnection, LobbyProtocol.State(State));
            }

            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Комната на стороне подключившегося (гостя): представляется серверу, получает состояние комнаты и чат,
    /// отправляет готовность и сообщения, узнаёт о старте матча.
    /// </summary>
    public sealed class LobbyGuest : ILobby
    {
        private readonly List<ChatLine> chat = new List<ChatLine>();
        private readonly Action<byte[]> send;

        public LobbyGuest(string playerName, Action<byte[]> send)
        {
            PlayerName = playerName;
            this.send = send ?? throw new ArgumentNullException(nameof(send));
        }

        public string PlayerName { get; }

        public LobbyState State { get; private set; }

        public bool IsJoined { get; private set; }

        public IReadOnlyList<ChatLine> Chat => chat;

        public bool IsHost => false;

        public bool HasPassword => false;

        public bool CanStart => false;

        public void SetMap(int map)
        {
        }

        public void SetNation(Nation nation)
        {
            send(LobbyProtocol.SetNation(nation));
        }

        public void SetFogOfWar(bool fog)
        {
        }

        public void StartMatch()
        {
        }

        public event Action Changed;

        public event Action<RejectReason> Rejected;

        public event Action<MatchSetup> Started;

        /// <summary>Соединение установлено — представиться (с хешем пароля, если он нужен).</summary>
        public void Join(string password)
        {
            send(LobbyProtocol.Hello(PlayerName, LobbyProtocol.HashPassword(password)));
        }

        public void Receive(byte[] message)
        {
            switch (LobbyProtocol.TypeOf(message))
            {
                case NetMessageType.Welcome:
                    IsJoined = true;
                    Changed?.Invoke();
                    break;
                case NetMessageType.Reject:
                    Rejected?.Invoke(LobbyProtocol.ReadReject(message));
                    break;
                case NetMessageType.LobbyState:
                    State = LobbyProtocol.ReadState(message);
                    Changed?.Invoke();
                    break;
                case NetMessageType.Chat:
                    LobbyProtocol.ReadChat(message, out string sender, out string text);
                    chat.Add(new ChatLine(sender, text));
                    if (chat.Count > LobbyHost.MaxChatLines)
                    {
                        chat.RemoveAt(0);
                    }

                    Changed?.Invoke();
                    break;
                case NetMessageType.StartMatch:
                    Started?.Invoke(LobbyProtocol.ReadStartMatch(message));
                    break;
            }
        }

        public void SetReady(bool ready)
        {
            send(LobbyProtocol.Ready(ready));
        }


        public void SendChat(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                send(LobbyProtocol.Chat(string.Empty, text.Trim()));
            }
        }

        public void Leave()
        {
            send(LobbyProtocol.Leave());
        }
    }
}
