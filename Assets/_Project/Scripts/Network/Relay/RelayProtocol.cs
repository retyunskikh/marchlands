using System.Collections.Generic;
using System.IO;
using Marchlands.Simulation.Core;

namespace Marchlands.Network.Relay
{
    /// <summary>Комната в списке сервера.</summary>
    public sealed class RelayRoomInfo
    {
        public int Id;
        public string Name;
        public string HostName;
        public bool HasPassword;
        public bool IsFull;
    }

    /// <summary>
    /// Сообщения сервера комнат (VPS): список комнат, создать, войти, настройки комнаты от создателя, просьба начать,
    /// «вы в комнате». Остальное (готовность, чат, старт, приказы, контрольные суммы) — сообщения <see cref="LobbyProtocol"/>
    /// и lockstep, сервер их пересылает.
    /// </summary>
    public static class RelayProtocol
    {
        public static byte[] ListRooms()
        {
            return LobbyProtocol.Build(NetMessageType.ListRooms, writer => { });
        }

        public static byte[] RoomList(IReadOnlyList<RelayRoomInfo> rooms)
        {
            return LobbyProtocol.Build(NetMessageType.RoomList, writer =>
            {
                writer.Write((ushort)rooms.Count);
                foreach (RelayRoomInfo room in rooms)
                {
                    writer.Write(room.Id);
                    writer.Write(room.Name ?? string.Empty);
                    writer.Write(room.HostName ?? string.Empty);
                    writer.Write(room.HasPassword);
                    writer.Write(room.IsFull);
                }
            });
        }

        public static List<RelayRoomInfo> ReadRoomList(byte[] message)
        {
            using (BinaryReader reader = LobbyProtocol.Open(message))
            {
                int count = reader.ReadUInt16();
                var rooms = new List<RelayRoomInfo>(count);
                for (int i = 0; i < count; i++)
                {
                    rooms.Add(new RelayRoomInfo
                    {
                        Id = reader.ReadInt32(),
                        Name = reader.ReadString(),
                        HostName = reader.ReadString(),
                        HasPassword = reader.ReadBoolean(),
                        IsFull = reader.ReadBoolean()
                    });
                }

                return rooms;
            }
        }

        public static byte[] CreateRoom(string playerName, string roomName, string passwordHash)
        {
            return LobbyProtocol.Build(NetMessageType.CreateRoom, writer =>
            {
                writer.Write(LobbyProtocol.Version);
                writer.Write(LobbyProtocol.Clip(playerName, LobbyProtocol.MaxNameLength));
                writer.Write(LobbyProtocol.Clip(roomName, LobbyProtocol.MaxNameLength));
                writer.Write(passwordHash ?? string.Empty);
            });
        }

        public static void ReadCreateRoom(byte[] message, out int version, out string playerName, out string roomName, out string passwordHash)
        {
            using (BinaryReader reader = LobbyProtocol.Open(message))
            {
                version = reader.ReadInt32();
                playerName = LobbyProtocol.Clip(reader.ReadString(), LobbyProtocol.MaxNameLength);
                roomName = LobbyProtocol.Clip(reader.ReadString(), LobbyProtocol.MaxNameLength);
                passwordHash = LobbyProtocol.Clip(reader.ReadString(), 64);
            }
        }

        public static byte[] JoinRoom(string playerName, int roomId, string passwordHash)
        {
            return LobbyProtocol.Build(NetMessageType.JoinRoom, writer =>
            {
                writer.Write(LobbyProtocol.Version);
                writer.Write(LobbyProtocol.Clip(playerName, LobbyProtocol.MaxNameLength));
                writer.Write(roomId);
                writer.Write(passwordHash ?? string.Empty);
            });
        }

        public static void ReadJoinRoom(byte[] message, out int version, out string playerName, out int roomId, out string passwordHash)
        {
            using (BinaryReader reader = LobbyProtocol.Open(message))
            {
                version = reader.ReadInt32();
                playerName = LobbyProtocol.Clip(reader.ReadString(), LobbyProtocol.MaxNameLength);
                roomId = reader.ReadInt32();
                passwordHash = LobbyProtocol.Clip(reader.ReadString(), 64);
            }
        }

        /// <summary>Создатель комнаты меняет карту, стороны или туман.</summary>
        public static byte[] RoomSettings(int map, Side hostSide, bool fogOfWar)
        {
            return LobbyProtocol.Build(NetMessageType.RoomSettings, writer =>
            {
                writer.Write(map);
                writer.Write((byte)hostSide);
                writer.Write(fogOfWar);
            });
        }

        public static void ReadRoomSettings(byte[] message, out int map, out Side hostSide, out bool fogOfWar)
        {
            using (BinaryReader reader = LobbyProtocol.Open(message))
            {
                map = reader.ReadInt32();
                hostSide = reader.ReadByte() == (byte)Side.Austria ? Side.Austria : Side.France;
                fogOfWar = reader.ReadBoolean();
            }
        }

        public static byte[] StartRequest()
        {
            return LobbyProtocol.Build(NetMessageType.StartRequest, writer => { });
        }

        /// <summary>Игрок в комнате: создатель (хост) или подключившийся.</summary>
        public static byte[] RoomJoined(bool isHost, bool hasPassword)
        {
            return LobbyProtocol.Build(NetMessageType.RoomJoined, writer =>
            {
                writer.Write(isHost);
                writer.Write(hasPassword);
            });
        }

        public static void ReadRoomJoined(byte[] message, out bool isHost, out bool hasPassword)
        {
            using (BinaryReader reader = LobbyProtocol.Open(message))
            {
                isHost = reader.ReadBoolean();
                hasPassword = reader.ReadBoolean();
            }
        }
    }
}
