using System;
using System.Collections.Generic;

namespace Marchlands.Network.Relay
{
    /// <summary>
    /// Игрок на сервере комнат: запрашивает список, создаёт комнату или входит в неё, а дальше — комната как в
    /// локальной сети (<see cref="ILobby"/>): готовность, чат, у создателя — карта, стороны, туман и старт.
    /// Соединение с сервером остаётся и после выхода из комнаты — можно войти в другую.
    /// </summary>
    public sealed class RelayLobby : ILobby
    {
        private readonly List<ChatLine> chat = new List<ChatLine>();
        private readonly Action<byte[]> send;

        public RelayLobby(string playerName, Action<byte[]> send)
        {
            PlayerName = playerName;
            this.send = send ?? throw new ArgumentNullException(nameof(send));
        }

        public string PlayerName { get; set; }

        public bool IsJoined { get; private set; }

        public LobbyState State { get; private set; }

        public bool IsHost { get; private set; }

        public bool HasPassword { get; private set; }

        public IReadOnlyList<ChatLine> Chat => chat;

        public bool CanStart => IsJoined && IsHost && State != null && State.HasGuest && State.HostReady && State.GuestReady;

        public event Action Changed;

        public event Action<MatchSetup> Started;

        /// <summary>Пришёл список комнат.</summary>
        public event Action<IReadOnlyList<RelayRoomInfo>> RoomsReceived;

        /// <summary>Сервер отказал (пароль, комната полна или закрыта и т. п.).</summary>
        public event Action<RejectReason> Rejected;

        public void RequestRooms()
        {
            send(RelayProtocol.ListRooms());
        }

        public void Create(string roomName, string password)
        {
            send(RelayProtocol.CreateRoom(PlayerName, roomName, LobbyProtocol.HashPassword(password)));
        }

        public void Join(int roomId, string password)
        {
            send(RelayProtocol.JoinRoom(PlayerName, roomId, LobbyProtocol.HashPassword(password)));
        }

        public void Receive(byte[] message)
        {
            switch (LobbyProtocol.TypeOf(message))
            {
                case NetMessageType.RoomList:
                    RoomsReceived?.Invoke(RelayProtocol.ReadRoomList(message));
                    break;
                case NetMessageType.RoomJoined:
                    RelayProtocol.ReadRoomJoined(message, out bool isHost, out bool hasPassword);
                    IsJoined = true;
                    IsHost = isHost;
                    HasPassword = hasPassword;
                    chat.Clear();
                    Changed?.Invoke();
                    break;
                case NetMessageType.Reject:
                    RejectReason reason = LobbyProtocol.ReadReject(message);
                    if (reason == RejectReason.RoomClosed)
                    {
                        ResetRoom();
                    }

                    Rejected?.Invoke(reason);
                    break;
                case NetMessageType.LobbyState:
                    if (IsJoined)
                    {
                        State = LobbyProtocol.ReadState(message);
                        Changed?.Invoke();
                    }

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
                    MatchSetup setup = LobbyProtocol.ReadStartMatch(message);
                    setup.IsHost = IsHost;
                    Started?.Invoke(setup);
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

        public void SetMap(int map)
        {
            if (IsHost && State != null)
            {
                send(RelayProtocol.RoomSettings(map, State.HostSide, State.FogOfWar));
            }
        }

        public void SwapSides()
        {
            if (IsHost && State != null)
            {
                send(RelayProtocol.RoomSettings(State.Map, State.GuestSide, State.FogOfWar));
            }
        }

        public void SetFogOfWar(bool fog)
        {
            if (IsHost && State != null)
            {
                send(RelayProtocol.RoomSettings(State.Map, State.HostSide, fog));
            }
        }

        public void StartMatch()
        {
            if (CanStart)
            {
                send(RelayProtocol.StartRequest());
            }
        }

        /// <summary>Выйти из комнаты (соединение с сервером остаётся).</summary>
        public void Leave()
        {
            if (IsJoined)
            {
                send(LobbyProtocol.Leave());
            }

            ResetRoom();
        }

        private void ResetRoom()
        {
            IsJoined = false;
            IsHost = false;
            State = null;
            chat.Clear();
        }
    }
}
