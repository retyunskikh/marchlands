using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Networking.Transport;

namespace Marchlands.Network
{
    /// <summary>
    /// Соединение по сети на Unity Transport (UDP с надёжной доставкой по порядку и разбивкой больших сообщений).
    /// Сервер слушает порт и принимает подключения, клиент подключается по адресу. Сообщения — массивы байт;
    /// события приходят из <see cref="Update"/>, которую нужно вызывать каждый кадр.
    /// </summary>
    public sealed class NetworkSession : IDisposable, ILobbyTransport
    {
        private readonly List<NetworkConnection> connections = new List<NetworkConnection>();
        private NetworkDriver driver;
        private NetworkPipeline pipeline;

        private NetworkSession(bool isHost, int disconnectTimeoutMs)
        {
            IsHost = isHost;
            var settings = new NetworkSettings();
            settings.WithNetworkConfigParameters(disconnectTimeoutMS: disconnectTimeoutMs, maxConnectAttempts: 10);
            driver = NetworkDriver.Create(settings);
            pipeline = driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
        }

        public bool IsHost { get; }

        /// <summary>Порт, на котором слушает сервер.</summary>
        public int Port { get; private set; }

        /// <summary>Подключилось новое соединение (у клиента — установлено соединение с сервером, номер 0).</summary>
        public event Action<int> Connected;

        public event Action<int, byte[]> DataReceived;

        public event Action<int> Disconnected;

        /// <summary>Сервер на порту; null — порт занят или недоступен.</summary>
        public static NetworkSession Host(int port, int disconnectTimeoutMs)
        {
            var session = new NetworkSession(true, disconnectTimeoutMs);
            if (session.driver.Bind(NetworkEndpoint.AnyIpv4.WithPort((ushort)port)) != 0 || session.driver.Listen() != 0)
            {
                session.Dispose();
                return null;
            }

            session.Port = port;
            return session;
        }

        /// <summary>Подключение к серверу по адресу (IPv4) и порту; null — адрес не разобрать.</summary>
        public static NetworkSession Join(string address, int port, int disconnectTimeoutMs)
        {
            if (!NetworkEndpoint.TryParse(address?.Trim(), (ushort)port, out NetworkEndpoint endpoint))
            {
                return null;
            }

            var session = new NetworkSession(false, disconnectTimeoutMs);
            session.Port = port;
            session.connections.Add(session.driver.Connect(endpoint));
            return session;
        }

        public void Update()
        {
            if (!driver.IsCreated)
            {
                return;
            }

            driver.ScheduleUpdate().Complete();

            if (IsHost)
            {
                NetworkConnection accepted;
                while ((accepted = driver.Accept()) != default)
                {
                    int index = FreeSlot();
                    connections[index] = accepted;
                    Connected?.Invoke(index);
                }
            }

            for (int i = 0; i < connections.Count; i++)
            {
                NetworkConnection connection = connections[i];
                if (!connection.IsCreated)
                {
                    continue;
                }

                NetworkEvent.Type type;
                while (driver.IsCreated && (type = driver.PopEventForConnection(connection, out DataStreamReader stream)) != NetworkEvent.Type.Empty)
                {
                    switch (type)
                    {
                        case NetworkEvent.Type.Connect:
                            Connected?.Invoke(i);
                            break;
                        case NetworkEvent.Type.Data:
                            var bytes = new NativeArray<byte>(stream.Length, Allocator.Temp);
                            stream.ReadBytes(bytes);
                            byte[] message = bytes.ToArray();
                            bytes.Dispose();
                            DataReceived?.Invoke(i, message);
                            break;
                        case NetworkEvent.Type.Disconnect:
                            connections[i] = default;
                            Disconnected?.Invoke(i);
                            break;
                    }

                    // Обработчик мог закрыть сессию (например, отказ в подключении) — дальше не читаем.
                    if (!driver.IsCreated)
                    {
                        return;
                    }

                    if (!connections[i].IsCreated)
                    {
                        break;
                    }
                }
            }
        }

        public bool IsConnected(int connection)
        {
            return driver.IsCreated && connection >= 0 && connection < connections.Count && connections[connection].IsCreated
                && driver.GetConnectionState(connections[connection]) == NetworkConnection.State.Connected;
        }

        /// <summary>
        /// Измеренный транспортом пинг соединения (туда-обратно, сглаженный) и его разброс, мс; false — ещё не измерен.
        /// </summary>
        public bool TryGetLatency(int connection, out float roundTripMs, out float jitterMs)
        {
            roundTripMs = 0f;
            jitterMs = 0f;
            if (!IsConnected(connection))
            {
                return false;
            }

            var latency = driver.GetConnectionStatistics(connections[connection]).Latency;
            roundTripMs = latency.SmoothedCurrent;
            jitterMs = latency.StandardDeviation;
            return roundTripMs > 0f;
        }

        public void Send(int connection, byte[] message)
        {
            if (!IsConnected(connection) || message == null)
            {
                return;
            }

            if (driver.BeginSend(pipeline, connections[connection], out DataStreamWriter writer) != 0)
            {
                return;
            }

            var bytes = new NativeArray<byte>(message, Allocator.Temp);
            writer.WriteBytes(bytes);
            bytes.Dispose();
            driver.EndSend(writer);
        }

        public void Disconnect(int connection)
        {
            if (driver.IsCreated && connection >= 0 && connection < connections.Count && connections[connection].IsCreated)
            {
                // Отложенное отключение: дать уйти последнему сообщению (например, отказу в подключении).
                driver.ScheduleFlushSend(default).Complete();
                driver.Disconnect(connections[connection]);
                connections[connection] = default;
            }
        }

        public void Dispose()
        {
            if (driver.IsCreated)
            {
                for (int i = 0; i < connections.Count; i++)
                {
                    if (connections[i].IsCreated)
                    {
                        driver.Disconnect(connections[i]);
                    }
                }

                driver.ScheduleUpdate().Complete();
                driver.Dispose();
            }

            connections.Clear();
        }

        private int FreeSlot()
        {
            for (int i = 0; i < connections.Count; i++)
            {
                if (!connections[i].IsCreated)
                {
                    return i;
                }
            }

            connections.Add(default);
            return connections.Count - 1;
        }
    }
}
