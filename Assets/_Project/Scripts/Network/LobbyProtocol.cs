using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Marchlands.Simulation.Core;

namespace Marchlands.Network
{
    /// <summary>Почему сервер не пустил игрока.</summary>
    public enum RejectReason : byte
    {
        WrongPassword = 1,
        Full = 2,
        VersionMismatch = 3,
        InGame = 4,
        RoomNotFound = 5,
        RoomClosed = 6,
        TooManyRooms = 7
    }

    /// <summary>Что видно в комнате: сервер, игроки, карта, стороны, туман, готовность.</summary>
    public sealed class LobbyState
    {
        public string ServerName = string.Empty;
        public string HostName = string.Empty;
        public string GuestName = string.Empty;
        public int Map;
        public Side HostSide = Side.France;
        public bool FogOfWar = true;
        public bool HostReady;
        public bool GuestReady;

        public bool HasGuest => !string.IsNullOrEmpty(GuestName);

        public Side GuestSide => HostSide == Side.France ? Side.Austria : Side.France;
    }

    /// <summary>С чем начинается сетевой матч: у обеих машин одинаковое, кроме своей стороны.</summary>
    public sealed class MatchSetup
    {
        public ulong Seed;
        public int Map;
        public Side HostSide;
        public bool FogOfWar;
        public bool IsHost;

        public Side LocalSide => IsHost ? HostSide : (HostSide == Side.France ? Side.Austria : Side.France);
    }

    /// <summary>
    /// Сообщения лобби в байты и обратно. Пароль по сети не передаётся — только его хеш.
    /// Версия протокола меняется при любом несовместимом изменении сообщений или симуляции.
    /// </summary>
    public static class LobbyProtocol
    {
        public const int Version = 1;
        internal const int MaxNameLength = 32;
        private const int MaxChatLength = 200;

        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return string.Empty;
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes("marchlands:" + password));
                return Convert.ToBase64String(hash);
            }
        }

        public static NetMessageType TypeOf(byte[] message)
        {
            return message != null && message.Length > 0 ? (NetMessageType)message[0] : 0;
        }

        public static byte[] Hello(string playerName, string passwordHash)
        {
            return Build(NetMessageType.Hello, writer =>
            {
                writer.Write(Version);
                writer.Write(Clip(playerName, MaxNameLength));
                writer.Write(passwordHash ?? string.Empty);
            });
        }

        public static void ReadHello(byte[] message, out int version, out string playerName, out string passwordHash)
        {
            using (BinaryReader reader = Open(message))
            {
                version = reader.ReadInt32();
                playerName = Clip(reader.ReadString(), MaxNameLength);
                passwordHash = reader.ReadString();
            }
        }

        public static byte[] Welcome()
        {
            return Build(NetMessageType.Welcome, writer => { });
        }

        public static byte[] Reject(RejectReason reason)
        {
            return Build(NetMessageType.Reject, writer => writer.Write((byte)reason));
        }

        public static RejectReason ReadReject(byte[] message)
        {
            using (BinaryReader reader = Open(message))
            {
                return (RejectReason)reader.ReadByte();
            }
        }

        public static byte[] State(LobbyState state)
        {
            return Build(NetMessageType.LobbyState, writer =>
            {
                writer.Write(state.ServerName ?? string.Empty);
                writer.Write(state.HostName ?? string.Empty);
                writer.Write(state.GuestName ?? string.Empty);
                writer.Write(state.Map);
                writer.Write((byte)state.HostSide);
                writer.Write(state.FogOfWar);
                writer.Write(state.HostReady);
                writer.Write(state.GuestReady);
            });
        }

        public static LobbyState ReadState(byte[] message)
        {
            using (BinaryReader reader = Open(message))
            {
                return new LobbyState
                {
                    ServerName = reader.ReadString(),
                    HostName = reader.ReadString(),
                    GuestName = reader.ReadString(),
                    Map = reader.ReadInt32(),
                    HostSide = reader.ReadByte() == (byte)Side.Austria ? Side.Austria : Side.France,
                    FogOfWar = reader.ReadBoolean(),
                    HostReady = reader.ReadBoolean(),
                    GuestReady = reader.ReadBoolean()
                };
            }
        }

        public static byte[] Ready(bool ready)
        {
            return Build(NetMessageType.Ready, writer => writer.Write(ready));
        }

        public static bool ReadReady(byte[] message)
        {
            using (BinaryReader reader = Open(message))
            {
                return reader.ReadBoolean();
            }
        }

        /// <summary>Строка чата: от гостя к серверу — без имени (его подставит сервер), от сервера — с именем.</summary>
        public static byte[] Chat(string sender, string text)
        {
            return Build(NetMessageType.Chat, writer =>
            {
                writer.Write(Clip(sender, MaxNameLength));
                writer.Write(Clip(text, MaxChatLength));
            });
        }

        public static void ReadChat(byte[] message, out string sender, out string text)
        {
            using (BinaryReader reader = Open(message))
            {
                sender = Clip(reader.ReadString(), MaxNameLength);
                text = Clip(reader.ReadString(), MaxChatLength);
            }
        }

        public static byte[] StartMatch(MatchSetup setup)
        {
            return Build(NetMessageType.StartMatch, writer =>
            {
                writer.Write(setup.Seed);
                writer.Write(setup.Map);
                writer.Write((byte)setup.HostSide);
                writer.Write(setup.FogOfWar);
            });
        }

        /// <summary>Параметры матча у гостя (IsHost = false).</summary>
        public static MatchSetup ReadStartMatch(byte[] message)
        {
            using (BinaryReader reader = Open(message))
            {
                return new MatchSetup
                {
                    Seed = reader.ReadUInt64(),
                    Map = reader.ReadInt32(),
                    HostSide = reader.ReadByte() == (byte)Side.Austria ? Side.Austria : Side.France,
                    FogOfWar = reader.ReadBoolean(),
                    IsHost = false
                };
            }
        }

        public static byte[] Leave()
        {
            return Build(NetMessageType.Leave, writer => { });
        }

        public static byte[] Quit()
        {
            return Build(NetMessageType.Quit, writer => { });
        }

        internal static byte[] Build(NetMessageType type, Action<BinaryWriter> body)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write((byte)type);
                body(writer);
                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>Читатель тела сообщения (после байта типа).</summary>
        internal static BinaryReader Open(byte[] message)
        {
            var reader = new BinaryReader(new MemoryStream(message), Encoding.UTF8);
            reader.ReadByte();
            return reader;
        }

        internal static string Clip(string value, int length)
        {
            value = value ?? string.Empty;
            return value.Length <= length ? value : value.Substring(0, length);
        }
    }
}
