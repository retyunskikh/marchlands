using System;
using System.Collections.Generic;
using UnityEngine;

namespace Marchlands.Network
{
    /// <summary>
    /// Сетевая игра между сценами: держит соединение (лобби в меню, затем матч в сцене боя), каждый кадр прокачивает
    /// его и раздаёт сообщения текущему обработчику. Пока обработчика нет (сцена боя ещё грузится), сообщения копятся.
    /// </summary>
    public sealed class NetworkGame : MonoBehaviour
    {
        private static NetworkGame instance;
        private static readonly Queue<KeyValuePair<int, byte[]>> buffered = new Queue<KeyValuePair<int, byte[]>>();
        private static Action<int, byte[]> handler;

        public static NetworkSession Session { get; private set; }

        public static LobbyHost Host { get; set; }

        public static LobbyGuest Guest { get; set; }

        /// <summary>Параметры начатого сетевого матча; null — обычная игра против ИИ.</summary>
        public static MatchSetup Setup { get; set; }

        public static bool IsActive => Session != null;

        /// <summary>Номер соединения с соперником: у хоста — гость, у гостя — сервер.</summary>
        public static int PeerConnection => Session != null && Session.IsHost ? (Host != null ? Host.GuestConnection : -1) : 0;

        /// <summary>Соединение с соперником (или у гостя — с сервером) оборвалось.</summary>
        public static event Action<int> Disconnected;

        public static event Action<int> Connected;

        /// <summary>Кто получает сообщения; при смене обработчика накопленное сразу отдаётся ему.</summary>
        public static Action<int, byte[]> Handler
        {
            get => handler;
            set
            {
                handler = value;
                while (handler != null && buffered.Count > 0)
                {
                    KeyValuePair<int, byte[]> message = buffered.Dequeue();
                    handler(message.Key, message.Value);
                }
            }
        }

        /// <summary>Начинает сетевую игру с этим соединением (предыдущее закрывается).</summary>
        public static void Begin(NetworkSession session)
        {
            Shutdown();
            // Свёрнутое окно не должно останавливать игру: соперник ждал бы каждый наш тик.
            Application.runInBackground = true;
            Session = session;
            session.DataReceived += OnData;
            session.Disconnected += connection => Disconnected?.Invoke(connection);
            session.Connected += connection => Connected?.Invoke(connection);
            if (instance == null)
            {
                var runner = new GameObject("Network");
                DontDestroyOnLoad(runner);
                instance = runner.AddComponent<NetworkGame>();
            }
        }

        /// <summary>Отправить сопернику.</summary>
        public static void SendToPeer(byte[] message)
        {
            Session?.Send(PeerConnection, message);
        }

        /// <summary>Закрывает соединение и забывает лобби и матч.</summary>
        public static void Shutdown()
        {
            Session?.Dispose();

            Session = null;
            Host = null;
            Guest = null;
            Setup = null;
            handler = null;
            buffered.Clear();
            Disconnected = null;
            Connected = null;
        }

        private static void OnData(int connection, byte[] message)
        {
            if (handler != null)
            {
                handler(connection, message);
            }
            else
            {
                buffered.Enqueue(new KeyValuePair<int, byte[]>(connection, message));
            }
        }

        private void Update()
        {
            Session?.Update();
        }

        private void OnApplicationQuit()
        {
            if (Session != null && Setup != null)
            {
                SendToPeer(LobbyProtocol.Quit());
            }
            else if (Guest != null)
            {
                Guest.Leave();
            }

            Shutdown();
        }
    }
}
