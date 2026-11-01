using System.Collections.Generic;
using System.Linq;
using Marchlands.Network;
using Marchlands.Network.Relay;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    /// <summary>Сервер комнат (VPS) без сети: комнаты, пароль, готовность, старт, пересылка боя, уход игрока.</summary>
    public class RelayServerTests
    {
        /// <summary>Сервер и игроки в одной памяти: сообщения доставляются сразу.</summary>
        private sealed class Net : ILobbyTransport
        {
            public RelayServer Server;
            public readonly Dictionary<int, RelayLobby> Clients = new Dictionary<int, RelayLobby>();
            public readonly Dictionary<int, List<byte[]>> Inbox = new Dictionary<int, List<byte[]>>();
            public double Time;

            public void Send(int connection, byte[] message)
            {
                if (!Inbox.ContainsKey(connection))
                {
                    Inbox[connection] = new List<byte[]>();
                }

                Inbox[connection].Add(message);
                if (Clients.TryGetValue(connection, out RelayLobby client))
                {
                    client.Receive(message);
                }
            }

            public void Disconnect(int connection)
            {
            }

            public RelayLobby Connect(int connection, string name)
            {
                var client = new RelayLobby(name, message => Server.Receive(connection, message));
                Clients[connection] = client;
                return client;
            }

            public int Count(int connection, NetMessageType type)
            {
                return Inbox.TryGetValue(connection, out List<byte[]> list) ? list.Count(m => LobbyProtocol.TypeOf(m) == type) : 0;
            }
        }

        private static Net CreateNet()
        {
            var net = new Net();
            ulong seed = 1000;
            net.Server = new RelayServer(net, () => seed++, () => net.Time) { MapCount = 4 };
            return net;
        }

        [Test]
        public void CreateListJoin_WithPassword()
        {
            Net net = CreateNet();
            RelayLobby alice = net.Connect(1, "Алиса");
            RelayLobby bob = net.Connect(2, "Боб");
            RelayLobby carol = net.Connect(3, "Кэрол");

            alice.Create("Битва", "секрет");
            Assert.IsTrue(alice.IsJoined && alice.IsHost && alice.HasPassword);
            Assert.AreEqual("Битва", alice.State.ServerName);

            IReadOnlyList<RelayRoomInfo> rooms = null;
            bob.RoomsReceived += list => rooms = list;
            bob.RequestRooms();
            Assert.AreEqual(1, rooms.Count);
            Assert.IsTrue(rooms[0].HasPassword);
            Assert.AreEqual("Алиса", rooms[0].HostName);

            RejectReason? rejected = null;
            bob.Rejected += reason => rejected = reason;
            bob.Join(rooms[0].Id, "не тот");
            Assert.AreEqual(RejectReason.WrongPassword, rejected);
            Assert.IsFalse(bob.IsJoined);

            bob.Join(rooms[0].Id, "секрет");
            Assert.IsTrue(bob.IsJoined);
            Assert.IsFalse(bob.IsHost);
            Assert.AreEqual("Боб", alice.State.GuestName);
            Assert.AreEqual("Алиса", bob.State.HostName);

            RejectReason? full = null;
            carol.Rejected += reason => full = reason;
            carol.Join(rooms[0].Id, "секрет");
            Assert.AreEqual(RejectReason.Full, full);

            rooms = null;
            carol.RoomsReceived += list => rooms = list;
            carol.RequestRooms();
            Assert.IsTrue(rooms[0].IsFull);
        }

        [Test]
        public void TwoRooms_ChatGoesOnlyToOwnRoom()
        {
            Net net = CreateNet();
            RelayLobby a1 = net.Connect(1, "А1");
            RelayLobby a2 = net.Connect(2, "А2");
            RelayLobby b1 = net.Connect(3, "Б1");
            RelayLobby b2 = net.Connect(4, "Б2");
            a1.Create("А", string.Empty);
            b1.Create("Б", string.Empty);
            Assert.AreEqual(2, net.Server.RoomCount);
            a2.Join(1, string.Empty);
            b2.Join(2, string.Empty);

            a2.SendChat("Привет из А");
            Assert.AreEqual("А2", a1.Chat.Last().Sender);
            Assert.AreEqual("Привет из А", a1.Chat.Last().Text);
            Assert.AreEqual(0, b1.Chat.Count);
            Assert.AreEqual(0, b2.Chat.Count);

            // Слишком часто — второе сообщение отброшено.
            a2.SendChat("ещё");
            Assert.AreEqual(1, a1.Chat.Count);
            net.Time += 1.0;
            a2.SendChat("ещё");
            Assert.AreEqual(2, a1.Chat.Count);
        }

        [Test]
        public void OnlyHostChangesSettings_ChangeResetsReady_StartWhenBothReady()
        {
            Net net = CreateNet();
            RelayLobby host = net.Connect(1, "Хост");
            RelayLobby guest = net.Connect(2, "Гость");
            host.Create("Комната", string.Empty);
            guest.Join(1, string.Empty);

            // Гость не может менять настройки.
            net.Server.Receive(2, RelayProtocol.RoomSettings(3, Side.Austria, false));
            Assert.AreEqual(0, host.State.Map);

            host.SetReady(true);
            guest.SetReady(true);
            Assert.IsTrue(host.CanStart);
            host.SetMap(2);
            Assert.AreEqual(2, guest.State.Map);
            Assert.IsFalse(host.State.HostReady || host.State.GuestReady, "смена карты снимает готовность");
            Assert.IsFalse(host.CanStart);
            host.SetMap(99);
            Assert.AreEqual(3, host.State.Map, "номер карты ограничен");

            host.SwapSides();
            host.SetFogOfWar(false);
            Assert.AreEqual(Side.Austria, guest.State.HostSide);
            Assert.IsFalse(guest.State.FogOfWar);

            MatchSetup hostSetup = null;
            MatchSetup guestSetup = null;
            host.Started += setup => hostSetup = setup;
            guest.Started += setup => guestSetup = setup;
            net.Server.Receive(2, RelayProtocol.StartRequest());
            Assert.IsNull(hostSetup, "гость начать не может");
            host.StartMatch();
            Assert.IsNull(hostSetup, "не готовы — не начинается");

            host.SetReady(true);
            guest.SetReady(true);
            host.StartMatch();
            Assert.IsNotNull(hostSetup);
            Assert.AreEqual(hostSetup.Seed, guestSetup.Seed);
            Assert.AreEqual(3, guestSetup.Map);
            Assert.IsFalse(guestSetup.FogOfWar);
            Assert.AreEqual(Side.Austria, hostSetup.LocalSide);
            Assert.AreEqual(Side.France, guestSetup.LocalSide);
        }

        [Test]
        public void EachPlayerChoosesNation_DefaultFrance_ResetsReady_PassedToMatch()
        {
            Net net = CreateNet();
            RelayLobby host = net.Connect(1, "Хост");
            RelayLobby guest = net.Connect(2, "Гость");
            host.Create("Комната", string.Empty);
            guest.Join(1, string.Empty);
            Assert.AreEqual(Nation.France, host.State.HostNation, "по умолчанию — Франция");
            Assert.AreEqual(Nation.France, host.State.GuestNation);

            host.SetReady(true);
            guest.SetReady(true);
            guest.SetNation(Nation.Austria);
            Assert.AreEqual(Nation.Austria, host.State.GuestNation, "гость выбрал сам");
            Assert.AreEqual(Nation.France, host.State.HostNation);
            Assert.IsFalse(host.State.HostReady || host.State.GuestReady, "смена нации снимает готовность");

            guest.SetNation(Nation.France);
            host.SetReady(true);
            guest.SetReady(true);
            MatchSetup hostSetup = null;
            MatchSetup guestSetup = null;
            host.Started += setup => hostSetup = setup;
            guest.Started += setup => guestSetup = setup;
            host.StartMatch();
            Assert.AreEqual(Nation.France, guestSetup.NationOf(Side.France));
            Assert.AreEqual(Nation.France, guestSetup.NationOf(Side.Austria), "обе стороны — французы");
            Assert.AreEqual(hostSetup.HostNation, guestSetup.HostNation);
        }

        [Test]
        public void HostLeavesBeforeMatch_RoomClosed_GuestLeaves_RoomStays()
        {
            Net net = CreateNet();
            RelayLobby host = net.Connect(1, "Хост");
            RelayLobby guest = net.Connect(2, "Гость");
            host.Create("Комната", string.Empty);
            guest.Join(1, string.Empty);

            guest.Leave();
            Assert.IsFalse(host.State.HasGuest);
            Assert.AreEqual(1, net.Server.RoomCount);

            guest.Join(1, string.Empty);
            RejectReason? reason = null;
            guest.Rejected += r => reason = r;
            net.Server.OnDisconnected(1);
            Assert.AreEqual(RejectReason.RoomClosed, reason);
            Assert.IsFalse(guest.IsJoined);
            Assert.AreEqual(0, net.Server.RoomCount);
        }

        [Test]
        public void Match_ForwardsOnlyToOpponent_AndQuitEndsIt()
        {
            Net net = CreateNet();
            RelayLobby a = net.Connect(1, "А");
            RelayLobby b = net.Connect(2, "Б");
            RelayLobby c = net.Connect(3, "В");
            RelayLobby d = net.Connect(4, "Г");
            a.Create("1", string.Empty);
            c.Create("2", string.Empty);
            b.Join(1, string.Empty);
            d.Join(2, string.Empty);
            foreach (RelayLobby lobby in new[] { a, b })
            {
                lobby.SetReady(true);
            }

            a.StartMatch();
            byte[] input = LockstepSession.EncodeInput(5, new ICommand[0]);
            net.Server.Receive(1, input);
            Assert.AreEqual(1, net.Count(2, NetMessageType.TickInput));
            Assert.AreEqual(0, net.Count(1, NetMessageType.TickInput), "себе не возвращается");
            Assert.AreEqual(0, net.Count(3, NetMessageType.TickInput) + net.Count(4, NetMessageType.TickInput), "в чужую комнату не уходит");

            // Приказы до старта не пересылаются.
            net.Server.Receive(3, input);
            Assert.AreEqual(0, net.Count(4, NetMessageType.TickInput));

            // Обрыв связи в бою: сопернику — «покинул бой», комната закрыта.
            net.Server.OnDisconnected(2);
            Assert.AreEqual(1, net.Count(1, NetMessageType.Quit));
            Assert.AreEqual(1, net.Server.RoomCount);
        }

        [Test]
        public void Lockstep_ThroughRelay_StaysInSync()
        {
            Net net = CreateNet();
            var sessions = new Dictionary<int, LockstepSession>();
            var queues = new Dictionary<int, Queue<byte[]>> { [1] = new Queue<byte[]>(), [2] = new Queue<byte[]>() };
            var transport = new QueueTransport(queues);
            ulong seed = 7;
            var server = new RelayServer(transport, () => seed, () => 0.0);

            // Комната и старт прямо через протокол.
            server.Receive(1, RelayProtocol.CreateRoom("А", "Комната", string.Empty));
            server.Receive(2, RelayProtocol.JoinRoom("Б", 1, string.Empty));
            server.Receive(1, LobbyProtocol.Ready(true));
            server.Receive(2, LobbyProtocol.Ready(true));
            server.Receive(1, RelayProtocol.StartRequest());
            queues[1].Clear();
            queues[2].Clear();

            Match france = CreateTwin(seed);
            Match austria = CreateTwin(seed);
            var franceSession = new LockstepSession(france, Side.France, 4, 10, new ToServer(server, 1));
            var austriaSession = new LockstepSession(austria, Side.Austria, 4, 10, new ToServer(server, 2));
            bool desync = false;
            franceSession.Desynced += () => desync = true;
            austriaSession.Desynced += () => desync = true;
            Squad frenchSquad = france.Squads.First(s => s.Side == Side.France);
            Squad austrianSquad = austria.Squads.First(s => s.Side == Side.Austria);
            franceSession.Submit(new MoveCommand(frenchSquad.Id, new float2(0f, 20f), 90f));
            austriaSession.Submit(new MoveCommand(austrianSquad.Id, new float2(0f, 25f), 270f));

            for (int frame = 0; frame < 2000 && (france.CurrentTick < 300 || austria.CurrentTick < 300); frame++)
            {
                franceSession.TryAdvance();
                austriaSession.TryAdvance();
                while (queues[1].Count > 0)
                {
                    franceSession.Receive(queues[1].Dequeue());
                }

                while (queues[2].Count > 0)
                {
                    austriaSession.Receive(queues[2].Dequeue());
                }
            }

            Assert.IsFalse(desync);
            Assert.GreaterOrEqual(france.CurrentTick, 300);
            Assert.AreEqual(france.CurrentTick, austria.CurrentTick);
            Assert.AreEqual(StateHash.Of(france), StateHash.Of(austria));
            Assert.Less(france.Squads.First(s => s.Side == Side.Austria).Position.y, 39f, "приказ австрийца исполнился и у француза");
        }

        private sealed class QueueTransport : ILobbyTransport
        {
            private readonly Dictionary<int, Queue<byte[]>> queues;

            public QueueTransport(Dictionary<int, Queue<byte[]>> queues)
            {
                this.queues = queues;
            }

            public void Send(int connection, byte[] message)
            {
                queues[connection].Enqueue(message);
            }

            public void Disconnect(int connection)
            {
            }
        }

        private sealed class ToServer : ILockstepLink
        {
            private readonly RelayServer server;
            private readonly int connection;

            public ToServer(RelayServer server, int connection)
            {
                this.server = server;
                this.connection = connection;
            }

            public void Send(byte[] message)
            {
                server.Receive(connection, message);
            }
        }

        private static Match CreateTwin(ulong seed)
        {
            var match = new Match(seed, TestSettings.Create());
            match.AddSquad(Side.France, new float2(-10f, 0f), 90f, Formation.Line);
            match.AddSquad(Side.Austria, new float2(10f, 40f), 270f, Formation.Line);
            return match;
        }
    }
}
