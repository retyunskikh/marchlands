using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Marchlands.Network
{
    /// <summary>Сервер в локальной сети, найденный по объявлению.</summary>
    public sealed class LanServer
    {
        public string Name;
        public string Address;
        public int Port;
        public bool HasPassword;
        public bool IsFull;
        public double LastSeen;

        public string Key => Name + "|" + Port;
    }

    /// <summary>
    /// Поиск серверов в локальной сети: сервер раз в секунду рассылает широковещательное объявление (UDP) с именем,
    /// портом, признаком пароля и занятости; меню слушает объявления и держит список. Без потоков: опрос в Update.
    /// </summary>
    public sealed class LanDiscovery : IDisposable
    {
        private const string Magic = "MARCHLANDS";
        private readonly Dictionary<string, LanServer> servers = new Dictionary<string, LanServer>();
        private readonly int discoveryPort;
        private UdpClient listener;
        private UdpClient sender;
        private double nextAnnounce;

        public LanDiscovery(int discoveryPort)
        {
            this.discoveryPort = discoveryPort;
        }

        /// <summary>Сколько секунд сервер считается живым без нового объявления.</summary>
        public double ServerTimeout { get; set; } = 4.0;

        public double AnnounceInterval { get; set; } = 1.0;

        /// <summary>Начать слушать объявления (меню «Сетевая игра»). false — порт недоступен.</summary>
        public bool StartListening()
        {
            if (listener != null)
            {
                return true;
            }

            try
            {
                listener = new UdpClient();
                // Несколько копий игры на одном компьютере слушают один порт.
                listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listener.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));
                return true;
            }
            catch (SocketException)
            {
                listener?.Close();
                listener = null;
                return false;
            }
        }

        public IEnumerable<LanServer> Servers => servers.Values;

        /// <summary>Принимает объявления и забывает молчащие серверы. <paramref name="now"/> — время в секундах.</summary>
        public void Poll(double now)
        {
            if (listener != null)
            {
                try
                {
                    while (listener.Available > 0)
                    {
                        var from = new IPEndPoint(IPAddress.Any, 0);
                        byte[] data = listener.Receive(ref from);
                        LanServer server = Parse(data, from.Address.ToString());
                        if (server != null)
                        {
                            server.LastSeen = now;
                            servers[server.Key] = server;
                        }
                    }
                }
                catch (SocketException)
                {
                }
            }

            var stale = new List<string>();
            foreach (KeyValuePair<string, LanServer> pair in servers)
            {
                if (now - pair.Value.LastSeen > ServerTimeout)
                {
                    stale.Add(pair.Key);
                }
            }

            foreach (string key in stale)
            {
                servers.Remove(key);
            }
        }

        /// <summary>Сервер объявляет о себе (вызывать каждый кадр — рассылка не чаще AnnounceInterval).</summary>
        public void Announce(double now, string name, int port, bool hasPassword, bool isFull)
        {
            if (now < nextAnnounce)
            {
                return;
            }

            nextAnnounce = now + AnnounceInterval;
            try
            {
                if (sender == null)
                {
                    sender = new UdpClient { EnableBroadcast = true };
                }

                byte[] data = Encoding.UTF8.GetBytes(string.Join("\n", Magic, LobbyProtocol.Version.ToString(), port.ToString(),
                    hasPassword ? "1" : "0", isFull ? "1" : "0", name ?? string.Empty));
                sender.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, discoveryPort));
                // Копии игры на этом же компьютере видят сервер и без широковещания.
                sender.Send(data, data.Length, new IPEndPoint(IPAddress.Loopback, discoveryPort));
            }
            catch (SocketException)
            {
            }
        }

        public void Dispose()
        {
            listener?.Close();
            listener = null;
            sender?.Close();
            sender = null;
            servers.Clear();
        }

        private static LanServer Parse(byte[] data, string address)
        {
            string[] parts = Encoding.UTF8.GetString(data).Split(new[] { '\n' }, 6);
            if (parts.Length < 6 || parts[0] != Magic || parts[1] != LobbyProtocol.Version.ToString()
                || !int.TryParse(parts[2], out int port))
            {
                return null;
            }

            return new LanServer
            {
                Address = address,
                Port = port,
                HasPassword = parts[3] == "1",
                IsFull = parts[4] == "1",
                Name = parts[5]
            };
        }
    }
}
