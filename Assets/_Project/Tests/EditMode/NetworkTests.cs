using System.Collections.Generic;
using System.Linq;
using Marchlands.Network;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Marchlands.Tests.EditMode
{
    /// <summary>Сетевая игра без сети: кодек команд, lockstep двух машин, лобби.</summary>
    public class NetworkTests
    {
        private sealed class QueueLink : ILockstepLink
        {
            public readonly Queue<byte[]> Sent = new Queue<byte[]>();

            public void Send(byte[] message)
            {
                Sent.Enqueue(message);
            }
        }

        private sealed class RecordingTransport : ILobbyTransport
        {
            public readonly List<KeyValuePair<int, byte[]>> Sent = new List<KeyValuePair<int, byte[]>>();
            public readonly List<int> Disconnected = new List<int>();

            public void Send(int connection, byte[] message)
            {
                Sent.Add(new KeyValuePair<int, byte[]>(connection, message));
            }

            public void Disconnect(int connection)
            {
                Disconnected.Add(connection);
            }

            public byte[] Last(int connection, NetMessageType type)
            {
                return Sent.Where(m => m.Key == connection && LobbyProtocol.TypeOf(m.Value) == type).Select(m => m.Value).LastOrDefault();
            }
        }

        private static Match CreateTwin()
        {
            var match = new Match(42, TestSettings.Create());
            match.AddSquad(Side.France, new float2(-10f, 0f), 90f, Formation.Line);
            match.AddSquad(Side.France, new float2(10f, 0f), 90f, Formation.Line);
            match.AddSquad(Side.Austria, new float2(-10f, 40f), 270f, Formation.Line);
            match.AddSquad(Side.Austria, new float2(10f, 40f), 270f, Formation.Line);
            return match;
        }

        [Test]
        public void Codec_RoundTripsEveryCommand()
        {
            var commands = new ICommand[]
            {
                new MoveCommand(3, new float2(1.25f, -7.5f), 33.3f, true),
                new FaceCommand(4, 271.5f),
                new StopCommand(5),
                new VolleyCommand(6),
                new VolleyCommand(6, 12.5f),
                new ChargeCommand(7, 8),
                new TargetCommand(9, 10),
                new AssaultCommand(11, 12),
                new ChangeFormationCommand(13, Formation.Column),
                new SetShotModeCommand(14, ShotMode.RoundShot),
                new BuildBarracksCommand(Side.Austria, new float2(3f, 4f)),
                new BuildStableCommand(Side.France, new float2(-3f, 4f)),
                new BuildDepotCommand(Side.Austria, new float2(3f, -4f)),
                new BuildHouseCommand(Side.France),
                new SetHiringCommand(15, UnitType.Grenadiers, true),
                new FormSquadCommand(Side.Austria, UnitType.Cavalry),
                new DisbandReserveCommand(Side.France, UnitType.Fusiliers),
                new OrderGunCommand(16),
                new UpgradeBuildingCommand(17),
                new ResearchBalloonCommand(Side.Austria)
            };

            foreach (ICommand command in commands)
            {
                byte[] bytes = CommandCodec.ToBytes(command);
                ICommand copy = CommandCodec.FromBytes(bytes);
                Assert.AreEqual(command.GetType(), copy.GetType());
                CollectionAssert.AreEqual(bytes, CommandCodec.ToBytes(copy), command.GetType().Name);
            }

            var move = (MoveCommand)CommandCodec.FromBytes(CommandCodec.ToBytes(commands[0]));
            Assert.AreEqual(new float2(1.25f, -7.5f), move.Target);
            Assert.AreEqual(33.3f, move.FinalFacing);
            Assert.IsTrue(move.AutoRoute);
        }

        [Test]
        public void Lockstep_TwoMachines_StayInSync_WithLatency()
        {
            Match france = CreateTwin();
            Match austria = CreateTwin();
            var toAustria = new QueueLink();
            var toFrance = new QueueLink();
            var franceSession = new LockstepSession(france, Side.France, 3, 10, toAustria);
            var austriaSession = new LockstepSession(austria, Side.Austria, 3, 10, toFrance);
            bool desync = false;
            franceSession.Desynced += () => desync = true;
            austriaSession.Desynced += () => desync = true;

            var random = new System.Random(7);
            var inFlightToAustria = new Queue<KeyValuePair<int, byte[]>>();
            var inFlightToFrance = new Queue<KeyValuePair<int, byte[]>>();
            for (int frame = 0; frame < 3000 && (france.CurrentTick < 600 || austria.CurrentTick < 600); frame++)
            {
                // Обе стороны изредка отдают приказы своим отрядам.
                if (random.NextDouble() < 0.05)
                {
                    Squad squad = france.Squads.Where(s => s.Side == Side.France).ElementAt(random.Next(2));
                    franceSession.Submit(new MoveCommand(squad.Id, new float2(random.Next(-20, 20), random.Next(0, 30)), 90f));
                }

                if (random.NextDouble() < 0.05)
                {
                    Squad squad = austria.Squads.Where(s => s.Side == Side.Austria).ElementAt(random.Next(2));
                    austriaSession.Submit(random.NextDouble() < 0.5
                        ? (ICommand)new VolleyCommand(squad.Id)
                        : new MoveCommand(squad.Id, new float2(random.Next(-20, 20), random.Next(10, 40)), 270f));
                }

                // Машины идут неровно: то одна делает тик, то другая, то обе.
                if (random.NextDouble() < 0.8)
                {
                    franceSession.TryAdvance();
                }

                if (random.NextDouble() < 0.8)
                {
                    austriaSession.TryAdvance();
                }

                // Задержка сети: сообщение доходит через 0–4 кадра, по порядку.
                while (toAustria.Sent.Count > 0)
                {
                    inFlightToAustria.Enqueue(new KeyValuePair<int, byte[]>(frame + random.Next(0, 5), toAustria.Sent.Dequeue()));
                }

                while (toFrance.Sent.Count > 0)
                {
                    inFlightToFrance.Enqueue(new KeyValuePair<int, byte[]>(frame + random.Next(0, 5), toFrance.Sent.Dequeue()));
                }

                while (inFlightToAustria.Count > 0 && inFlightToAustria.Peek().Key <= frame)
                {
                    austriaSession.Receive(inFlightToAustria.Dequeue().Value);
                }

                while (inFlightToFrance.Count > 0 && inFlightToFrance.Peek().Key <= frame)
                {
                    franceSession.Receive(inFlightToFrance.Dequeue().Value);
                }
            }

            Assert.GreaterOrEqual(france.CurrentTick, 600);
            Assert.GreaterOrEqual(austria.CurrentTick, 600);
            Assert.IsFalse(desync, "контрольные суммы разошлись");

            // Доводим отстающую до того же тика и сравниваем целиком.
            while (france.CurrentTick != austria.CurrentTick)
            {
                LockstepSession behind = france.CurrentTick < austria.CurrentTick ? franceSession : austriaSession;
                while (toAustria.Sent.Count > 0)
                {
                    austriaSession.Receive(toAustria.Sent.Dequeue());
                }

                while (toFrance.Sent.Count > 0)
                {
                    franceSession.Receive(toFrance.Sent.Dequeue());
                }

                foreach (var pair in inFlightToAustria)
                {
                    austriaSession.Receive(pair.Value);
                }

                foreach (var pair in inFlightToFrance)
                {
                    franceSession.Receive(pair.Value);
                }

                inFlightToAustria.Clear();
                inFlightToFrance.Clear();
                Assert.IsTrue(behind.TryAdvance(), "отстающая машина должна догнать");
            }

            Assert.AreEqual(StateHash.Of(france), StateHash.Of(austria));
            Assert.IsTrue(france.Squads.Any(s => s.Side == Side.France && math.abs(s.Position.y) > 1f), "приказы исполнялись");
        }

        [Test]
        public void Lockstep_WaitsForOpponentInput()
        {
            Match match = CreateTwin();
            var session = new LockstepSession(match, Side.France, 3, 10, new QueueLink());

            for (int i = 0; i < 20; i++)
            {
                session.TryAdvance();
            }

            Assert.AreEqual(3, match.CurrentTick, "без приказов соперника — только первые inputDelay тиков");
            Assert.IsTrue(session.IsWaiting);

            session.Receive(LockstepSession.EncodeInput(3, new ICommand[0]));
            Assert.IsTrue(session.TryAdvance());
            Assert.AreEqual(4, match.CurrentTick);
        }

        [Test]
        public void Lockstep_OpponentCannotCommandOurSquads()
        {
            Match match = CreateTwin();
            Squad ours = match.Squads.First(s => s.Side == Side.France);
            float2 start = ours.Position;
            var session = new LockstepSession(match, Side.France, 1, 10, new QueueLink());
            session.TryAdvance();

            // «Австрия» прислала приказ французскому отряду.
            session.Receive(LockstepSession.EncodeInput(1, new ICommand[] { new MoveCommand(ours.Id, start + new float2(0f, 30f), 90f) }));
            for (int tick = 2; tick < 60; tick++)
            {
                session.Receive(LockstepSession.EncodeInput(tick, new ICommand[0]));
            }

            for (int i = 0; i < 60; i++)
            {
                session.TryAdvance();
            }

            Assert.Greater(match.CurrentTick, 50);
            Assert.AreEqual(start, ours.Position, "чужой приказ отброшен");
        }

        [Test]
        public void Lockstep_DetectsDesync()
        {
            Match france = CreateTwin();
            Match austria = CreateTwin();
            var toAustria = new QueueLink();
            var toFrance = new QueueLink();
            var a = new LockstepSession(france, Side.France, 2, 5, toAustria);
            var b = new LockstepSession(austria, Side.Austria, 2, 5, toFrance);
            austria.GetPlayer(Side.Austria).Gold += 1;

            for (int i = 0; i < 40; i++)
            {
                a.TryAdvance();
                b.TryAdvance();
                while (toAustria.Sent.Count > 0)
                {
                    b.Receive(toAustria.Sent.Dequeue());
                }

                while (toFrance.Sent.Count > 0)
                {
                    a.Receive(toFrance.Sent.Dequeue());
                }
            }

            Assert.IsTrue(a.IsDesynced && b.IsDesynced);
            Assert.AreEqual(5, a.DesyncTick);
        }

        [Test]
        public void Lobby_PasswordReadyChatAndStart()
        {
            var transport = new RecordingTransport();
            var host = new LobbyHost("Сервер", "секрет", "Хост", transport);
            Assert.IsTrue(host.HasPassword);

            host.Receive(1, LobbyProtocol.Hello("Чужой", LobbyProtocol.HashPassword("не тот")));
            Assert.AreEqual(RejectReason.WrongPassword, LobbyProtocol.ReadReject(transport.Last(1, NetMessageType.Reject)));
            Assert.Contains(1, transport.Disconnected);
            Assert.IsFalse(host.State.HasGuest);

            var toHost = new List<byte[]>();
            var guest = new LobbyGuest("Гость", toHost.Add);
            guest.Join("секрет");
            host.Receive(2, toHost[0]);
            Assert.AreEqual("Гость", host.State.GuestName);
            foreach (var message in transport.Sent.Where(m => m.Key == 2))
            {
                guest.Receive(message.Value);
            }

            Assert.IsTrue(guest.IsJoined);
            Assert.AreEqual("Хост", guest.State.HostName);

            // Третий лишний.
            host.Receive(3, LobbyProtocol.Hello("Третий", LobbyProtocol.HashPassword("секрет")));
            Assert.AreEqual(RejectReason.Full, LobbyProtocol.ReadReject(transport.Last(3, NetMessageType.Reject)));

            // Чат гостя доходит с его именем.
            guest.SendChat("Привет!");
            host.Receive(2, toHost.Last());
            Assert.AreEqual("Гость", host.Chat.Last().Sender);
            Assert.AreEqual("Привет!", host.Chat.Last().Text);

            // Готовность и сброс при смене карты.
            host.SetReady(true);
            guest.SetReady(true);
            host.Receive(2, toHost.Last());
            Assert.IsTrue(host.CanStart);
            host.SetMap(2);
            Assert.IsFalse(host.State.HostReady || host.State.GuestReady, "смена карты снимает готовность");
            Assert.IsNull(host.Start(1));

            host.SetReady(true);
            guest.SetReady(true);
            host.Receive(2, toHost.Last());
            host.SwapSides();
            host.SetReady(true);
            guest.SetReady(true);
            host.Receive(2, toHost.Last());
            MatchSetup hostSetup = host.Start(99);
            Assert.IsNotNull(hostSetup);

            MatchSetup guestSetup = null;
            guest.Started += setup => guestSetup = setup;
            guest.Receive(transport.Last(2, NetMessageType.StartMatch));
            Assert.AreEqual(99UL, guestSetup.Seed);
            Assert.AreEqual(2, guestSetup.Map);
            Assert.AreEqual(Side.Austria, hostSetup.LocalSide);
            Assert.AreEqual(Side.France, guestSetup.LocalSide);
        }

        [Test]
        public void Lobby_GuestChoosesNation_HostSeesIt()
        {
            var transport = new RecordingTransport();
            var host = new LobbyHost("Сервер", string.Empty, "Хост", transport);
            host.Receive(1, LobbyProtocol.Hello("Гость", string.Empty));
            host.SetReady(true);
            host.Receive(1, LobbyProtocol.SetNation(Nation.Austria));
            Assert.AreEqual(Nation.Austria, host.State.GuestNation);
            Assert.IsFalse(host.State.HostReady, "смена нации снимает готовность");
            host.SetNation(Nation.Austria);
            Assert.AreEqual(Nation.Austria, LobbyProtocol.ReadState(transport.Last(1, NetMessageType.LobbyState)).HostNation);
        }

        [Test]
        public void Lobby_NoPassword_AcceptsAnyone()
        {
            var transport = new RecordingTransport();
            var host = new LobbyHost("Сервер", string.Empty, "Хост", transport);
            Assert.IsFalse(host.HasPassword);
            host.Receive(1, LobbyProtocol.Hello("Гость", LobbyProtocol.HashPassword(string.Empty)));
            Assert.IsTrue(host.State.HasGuest);
            Assert.IsNotNull(transport.Last(1, NetMessageType.Welcome));
        }
    }
}
