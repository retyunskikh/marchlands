using System;
using System.Collections.Generic;
using Marchlands.Simulation.Core;

namespace Marchlands.Network.Relay
{
    /// <summary>
    /// Сервер комнат на VPS. Хранит комнаты (имя, пароль, два игрока, карта, стороны, туман, готовность, чат),
    /// отдаёт их список, пускает в комнату, по просьбе создателя начинает матч (общий seed — от сервера) и дальше
    /// только пересылает сообщения боя сопернику. Бой не считает. Ушёл игрок — сопернику «соперник покинул бой»,
    /// создатель вышел из комнаты до боя — комната закрывается.
    /// </summary>
    public sealed class RelayServer
    {
        /// <summary>Сообщение длиннее — мусор, не обрабатывается.</summary>
        public const int MaxMessageSize = 8192;

        private sealed class Room
        {
            public int Id;
            public string PasswordHash = string.Empty;
            public readonly LobbyState State = new LobbyState();
            public readonly List<ChatLine> Chat = new List<ChatLine>();
            public int Host = -1;
            public int Guest = -1;
            public bool Started;

            public int Other(int connection)
            {
                return connection == Host ? Guest : connection == Guest ? Host : -1;
            }
        }

        private readonly ILobbyTransport transport;
        private readonly Func<ulong> seedSource;
        private readonly Func<double> clock;
        private readonly Action<string> log;
        private readonly Dictionary<int, Room> rooms = new Dictionary<int, Room>();
        private readonly Dictionary<int, Room> roomOf = new Dictionary<int, Room>();
        private readonly Dictionary<int, double> lastChat = new Dictionary<int, double>();
        private int nextRoomId = 1;

        public RelayServer(ILobbyTransport transport, Func<ulong> seedSource, Func<double> clock, Action<string> log = null)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.seedSource = seedSource ?? throw new ArgumentNullException(nameof(seedSource));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.log = log;
        }

        /// <summary>Сколько комнат может быть на сервере.</summary>
        public int MaxRooms { get; set; } = 500;

        /// <summary>Сколько карт можно выбрать (номер карты из настроек ограничивается им).</summary>
        public int MapCount { get; set; } = 16;

        /// <summary>Не чаще одного сообщения чата за столько секунд с одного соединения.</summary>
        public double ChatInterval { get; set; } = 0.4;

        public int RoomCount => rooms.Count;

        public void Receive(int connection, byte[] message)
        {
            if (message == null || message.Length == 0 || message.Length > MaxMessageSize)
            {
                return;
            }

            try
            {
                Handle(connection, message);
            }
            catch (Exception exception) when (exception is System.IO.IOException || exception is ArgumentException
                                              || exception is InvalidOperationException || exception is FormatException)
            {
                // Испорченное сообщение (обрезанное, с недопустимыми значениями) — пропускаем, соединение не трогаем.
            }
        }

        public void OnDisconnected(int connection)
        {
            lastChat.Remove(connection);
            LeaveRoom(connection);
        }

        private void Handle(int connection, byte[] message)
        {
            NetMessageType type = LobbyProtocol.TypeOf(message);
            switch (type)
            {
                case NetMessageType.ListRooms:
                    SendRoomList(connection);
                    return;
                case NetMessageType.CreateRoom:
                    CreateRoom(connection, message);
                    return;
                case NetMessageType.JoinRoom:
                    JoinRoom(connection, message);
                    return;
            }

            if (!roomOf.TryGetValue(connection, out Room room))
            {
                return;
            }

            switch (type)
            {
                case NetMessageType.Leave:
                    LeaveRoom(connection);
                    break;
                case NetMessageType.Ready:
                    if (room.Started)
                    {
                        break;
                    }

                    bool ready = LobbyProtocol.ReadReady(message);
                    if (connection == room.Host)
                    {
                        room.State.HostReady = ready;
                    }
                    else
                    {
                        room.State.GuestReady = ready;
                    }

                    BroadcastState(room);
                    break;
                case NetMessageType.Chat:
                    Chat(connection, room, message);
                    break;
                case NetMessageType.RoomSettings:
                    if (connection != room.Host || room.Started)
                    {
                        break;
                    }

                    RelayProtocol.ReadRoomSettings(message, out int map, out Side hostSide, out bool fog);
                    room.State.Map = Math.Max(0, Math.Min(MapCount - 1, map));
                    room.State.HostSide = hostSide;
                    room.State.FogOfWar = fog;
                    room.State.HostReady = false;
                    room.State.GuestReady = false;
                    BroadcastState(room);
                    break;
                case NetMessageType.StartRequest:
                    StartMatch(connection, room);
                    break;
                case NetMessageType.TickInput:
                case NetMessageType.StateHash:
                    // Бой: приказы и контрольные суммы — сопернику как есть.
                    if (room.Started)
                    {
                        Send(room.Other(connection), message);
                    }

                    break;
                case NetMessageType.Quit:
                    if (room.Started)
                    {
                        LeaveRoom(connection);
                    }

                    break;
            }
        }

        private void SendRoomList(int connection)
        {
            var list = new List<RelayRoomInfo>();
            foreach (Room room in rooms.Values)
            {
                if (room.Started)
                {
                    continue;
                }

                list.Add(new RelayRoomInfo
                {
                    Id = room.Id,
                    Name = room.State.ServerName,
                    HostName = room.State.HostName,
                    HasPassword = !string.IsNullOrEmpty(room.PasswordHash),
                    IsFull = room.Guest >= 0
                });
            }

            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            Send(connection, RelayProtocol.RoomList(list));
        }

        private void CreateRoom(int connection, byte[] message)
        {
            RelayProtocol.ReadCreateRoom(message, out int version, out string playerName, out string roomName, out string passwordHash);
            if (version != LobbyProtocol.Version)
            {
                Send(connection, LobbyProtocol.Reject(RejectReason.VersionMismatch));
                return;
            }

            LeaveRoom(connection);
            if (rooms.Count >= MaxRooms)
            {
                Send(connection, LobbyProtocol.Reject(RejectReason.TooManyRooms));
                return;
            }

            var room = new Room { Id = nextRoomId++, PasswordHash = passwordHash ?? string.Empty, Host = connection };
            room.State.ServerName = string.IsNullOrWhiteSpace(roomName) ? "Комната " + room.Id : roomName.Trim();
            room.State.HostName = NameOr(playerName, "Игрок");
            rooms.Add(room.Id, room);
            roomOf[connection] = room;
            log?.Invoke($"Комната {room.Id} «{room.State.ServerName}» создана ({room.State.HostName}), комнат: {rooms.Count}");
            Send(connection, RelayProtocol.RoomJoined(true, !string.IsNullOrEmpty(room.PasswordHash)));
            BroadcastState(room);
        }

        private void JoinRoom(int connection, byte[] message)
        {
            RelayProtocol.ReadJoinRoom(message, out int version, out string playerName, out int roomId, out string passwordHash);
            rooms.TryGetValue(roomId, out Room room);
            RejectReason? reason = version != LobbyProtocol.Version ? RejectReason.VersionMismatch
                : room == null ? RejectReason.RoomNotFound
                : room.Started ? RejectReason.InGame
                : room.Guest >= 0 || room.Host == connection ? RejectReason.Full
                : (passwordHash ?? string.Empty) != room.PasswordHash ? RejectReason.WrongPassword
                : (RejectReason?)null;
            if (reason.HasValue)
            {
                Send(connection, LobbyProtocol.Reject(reason.Value));
                return;
            }

            LeaveRoom(connection);
            room.Guest = connection;
            room.State.GuestName = NameOr(playerName, "Гость");
            room.State.HostReady = false;
            room.State.GuestReady = false;
            roomOf[connection] = room;
            log?.Invoke($"В комнату {room.Id} вошёл {room.State.GuestName}");
            Send(connection, RelayProtocol.RoomJoined(false, !string.IsNullOrEmpty(room.PasswordHash)));
            foreach (ChatLine line in room.Chat)
            {
                Send(connection, LobbyProtocol.Chat(line.Sender, line.Text));
            }

            BroadcastState(room);
        }

        private void Chat(int connection, Room room, byte[] message)
        {
            double now = clock();
            if (lastChat.TryGetValue(connection, out double last) && now - last < ChatInterval)
            {
                return;
            }

            lastChat[connection] = now;
            LobbyProtocol.ReadChat(message, out _, out string text);
            text = text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            string sender = connection == room.Host ? room.State.HostName : room.State.GuestName;
            room.Chat.Add(new ChatLine(sender, text));
            if (room.Chat.Count > LobbyHost.MaxChatLines)
            {
                room.Chat.RemoveAt(0);
            }

            byte[] line = LobbyProtocol.Chat(sender, text);
            Send(room.Host, line);
            Send(room.Guest, line);
        }

        private void StartMatch(int connection, Room room)
        {
            if (connection != room.Host || room.Started || room.Guest < 0 || !room.State.HostReady || !room.State.GuestReady)
            {
                return;
            }

            room.Started = true;
            var setup = new MatchSetup
            {
                Seed = seedSource(),
                Map = room.State.Map,
                HostSide = room.State.HostSide,
                FogOfWar = room.State.FogOfWar
            };
            byte[] start = LobbyProtocol.StartMatch(setup);
            Send(room.Host, start);
            Send(room.Guest, start);
            log?.Invoke($"Комната {room.Id}: бой начат ({room.State.HostName} против {room.State.GuestName}), карта {setup.Map}");
        }

        /// <summary>Игрок уходит из своей комнаты (сам, разрывом связи или созданием/входом в другую).</summary>
        private void LeaveRoom(int connection)
        {
            if (!roomOf.TryGetValue(connection, out Room room))
            {
                return;
            }

            roomOf.Remove(connection);
            int other = room.Other(connection);
            if (room.Started)
            {
                // В бою: сопернику — «соперник покинул бой», комната закрывается.
                Send(other, LobbyProtocol.Quit());
                CloseRoom(room);
                return;
            }

            if (connection == room.Host)
            {
                // Создатель ушёл до боя — комната закрывается, гость возвращается к списку.
                Send(other, LobbyProtocol.Reject(RejectReason.RoomClosed));
                CloseRoom(room);
                return;
            }

            room.Guest = -1;
            room.State.GuestName = string.Empty;
            room.State.GuestReady = false;
            room.State.HostReady = false;
            BroadcastState(room);
        }

        private void CloseRoom(Room room)
        {
            roomOf.Remove(room.Host);
            roomOf.Remove(room.Guest);
            rooms.Remove(room.Id);
            log?.Invoke($"Комната {room.Id} закрыта, комнат: {rooms.Count}");
        }

        private void BroadcastState(Room room)
        {
            byte[] state = LobbyProtocol.State(room.State);
            Send(room.Host, state);
            Send(room.Guest, state);
        }

        private void Send(int connection, byte[] message)
        {
            if (connection >= 0)
            {
                transport.Send(connection, message);
            }
        }

        private static string NameOr(string name, string fallback)
        {
            return string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
        }
    }
}
