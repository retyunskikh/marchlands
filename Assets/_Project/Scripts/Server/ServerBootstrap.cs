using System;
using Marchlands.Network;
using Marchlands.Network.Relay;
using UnityEngine;

namespace Marchlands.Server
{
    /// <summary>
    /// Выделенный сервер комнат (сборка Linux Dedicated Server на VPS): слушает порт, держит комнаты и пересылает
    /// сообщения боя. Параметры командной строки: <c>-port 7777</c>, <c>-maxRooms 500</c>. Лог — в консоль (journalctl).
    /// </summary>
    public sealed class ServerBootstrap : MonoBehaviour
    {
        [SerializeField] private int port = 7777;
        [SerializeField] private int maxRooms = 500;
        [Tooltip("Сколько карт в игре: номер карты из комнаты ограничивается им.")]
        [SerializeField] private int mapCount = 4;
        [Tooltip("Через сколько миллисекунд без ответа игрок считается отключившимся.")]
        [SerializeField] private int disconnectTimeoutMs = 15000;
        [Tooltip("Кадров в секунду: серверу много не нужно, а процессор VPS бережём.")]
        [SerializeField] private int frameRate = 60;

        private NetworkSession session;
        private RelayServer relay;
        private readonly System.Random random = new System.Random();

        private void Awake()
        {
            Application.targetFrameRate = frameRate;
            Application.runInBackground = true;
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            port = ReadArgument("-port", port);
            maxRooms = ReadArgument("-maxRooms", maxRooms);

            session = NetworkSession.Host(port, disconnectTimeoutMs);
            if (session == null)
            {
                Debug.LogError($"Marchlands: не удалось открыть порт {port} (UDP) — занят?");
                Application.Quit(1);
                return;
            }

            relay = new RelayServer(session, NextSeed, () => Time.realtimeSinceStartupAsDouble, Log)
            {
                MaxRooms = maxRooms,
                MapCount = mapCount
            };
            session.DataReceived += relay.Receive;
            session.Connected += connection => Log($"Подключение {connection}");
            session.Disconnected += connection =>
            {
                Log($"Отключение {connection}");
                relay.OnDisconnected(connection);
            };
            Log($"Сервер Marchlands запущен: порт {port}/UDP, протокол {LobbyProtocol.Version}, комнат не больше {maxRooms}");
        }

        private void Update()
        {
            session?.Update();
        }

        private void OnApplicationQuit()
        {
            session?.Dispose();
            session = null;
        }

        private ulong NextSeed()
        {
            var bytes = new byte[8];
            random.NextBytes(bytes);
            return BitConverter.ToUInt64(bytes, 0);
        }

        private static void Log(string message)
        {
            Debug.Log($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {message}");
        }

        private static int ReadArgument(string name, int fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && int.TryParse(args[i + 1], out int value))
                {
                    return value;
                }
            }

            return fallback;
        }
    }
}
