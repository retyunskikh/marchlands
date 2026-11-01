using System;
using System.Collections.Generic;
using System.IO;
using Marchlands.Simulation.Commands;
using Marchlands.Simulation.Core;

namespace Marchlands.Network
{
    /// <summary>Канал до соперника: доставляет сообщения надёжно и по порядку.</summary>
    public interface ILockstepLink
    {
        void Send(byte[] message);
    }

    /// <summary>
    /// Lockstep: обе машины считают одну детерминированную симуляцию, а по сети передают только приказы.
    /// Приказ, отданный на тике T, исполняется на обеих машинах на тике T + inputDelay. Тик выполняется, только когда
    /// получены приказы обеих сторон на него (если соперник отстал — ждём, а не расходимся). Раз в hashInterval тиков
    /// стороны сверяют контрольную сумму состояния; расхождение — <see cref="Desynced"/>.
    /// </summary>
    public sealed class LockstepSession
    {
        private readonly Match match;
        private readonly ILockstepLink link;
        private readonly int inputDelay;
        private readonly int hashInterval;
        private readonly List<ICommand> pending = new List<ICommand>();
        private readonly Dictionary<int, List<ICommand>>[] inputs =
        {
            new Dictionary<int, List<ICommand>>(), new Dictionary<int, List<ICommand>>()
        };

        private readonly Dictionary<int, ulong> localHashes = new Dictionary<int, ulong>();
        private readonly Dictionary<int, ulong> remoteHashes = new Dictionary<int, ulong>();
        private readonly int firstTick;
        private int sentUpTo;

        public LockstepSession(Match match, Side localSide, int inputDelay, int hashInterval, ILockstepLink link)
        {
            this.match = match ?? throw new ArgumentNullException(nameof(match));
            this.link = link ?? throw new ArgumentNullException(nameof(link));
            LocalSide = localSide;
            this.inputDelay = Math.Max(1, inputDelay);
            this.hashInterval = Math.Max(1, hashInterval);
            firstTick = match.CurrentTick;
            sentUpTo = firstTick + this.inputDelay - 1;
        }

        public Side LocalSide { get; }

        public Side RemoteSide => LocalSide == Side.France ? Side.Austria : Side.France;

        /// <summary>Состояния разошлись: дальше играть нельзя.</summary>
        public bool IsDesynced { get; private set; }

        /// <summary>Тик, на котором впервые разошлась контрольная сумма.</summary>
        public int DesyncTick { get; private set; } = -1;

        public event Action Desynced;

        /// <summary>Последний вызов <see cref="TryAdvance"/> ждал приказов соперника.</summary>
        public bool IsWaiting { get; private set; }

        /// <summary>Приказ своего игрока: уйдёт сопернику и исполнится на обеих машинах на одном тике.</summary>
        public void Submit(ICommand command)
        {
            if (command != null)
            {
                pending.Add(command);
            }
        }

        /// <summary>Сообщение соперника (приказы на тик или контрольная сумма). Чужие типы сообщений — false.</summary>
        public bool Receive(byte[] message)
        {
            if (message == null || message.Length == 0)
            {
                return false;
            }

            using (var reader = new BinaryReader(new MemoryStream(message)))
            {
                var type = (NetMessageType)reader.ReadByte();
                switch (type)
                {
                    case NetMessageType.TickInput:
                        int tick = reader.ReadInt32();
                        int count = reader.ReadUInt16();
                        var commands = new List<ICommand>(count);
                        for (int i = 0; i < count; i++)
                        {
                            commands.Add(CommandCodec.Read(reader));
                        }

                        inputs[(int)RemoteSide][tick] = commands;
                        return true;
                    case NetMessageType.StateHash:
                        int hashTick = reader.ReadInt32();
                        remoteHashes[hashTick] = reader.ReadUInt64();
                        CompareHashes(hashTick);
                        return true;
                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Выполняет один тик, если приказы обеих сторон на него уже есть; иначе — false (ждём соперника).
        /// Свои приказы, накопленные с прошлого раза, отправляются на тик текущий + inputDelay.
        /// </summary>
        public bool TryAdvance()
        {
            if (IsDesynced)
            {
                return false;
            }

            int tick = match.CurrentTick;
            int closing = tick + inputDelay;
            if (sentUpTo < closing)
            {
                SendInput(closing);
            }

            if (!HasInput(Side.France, tick) || !HasInput(Side.Austria, tick))
            {
                IsWaiting = true;
                return false;
            }

            IsWaiting = false;
            // Порядок общий для обеих машин: сначала приказы Франции, затем Австрии, каждая — в порядке отдачи.
            Schedule(Side.France, tick);
            Schedule(Side.Austria, tick);
            match.Tick();

            if (match.CurrentTick % hashInterval == 0)
            {
                int hashTick = match.CurrentTick;
                localHashes[hashTick] = StateHash.Of(match);
                link.Send(EncodeHash(hashTick, localHashes[hashTick]));
                CompareHashes(hashTick);
            }

            return true;
        }

        private void SendInput(int tick)
        {
            var commands = new List<ICommand>(pending);
            pending.Clear();
            inputs[(int)LocalSide][tick] = commands;
            sentUpTo = tick;
            link.Send(EncodeInput(tick, commands));
        }

        private bool HasInput(Side side, int tick)
        {
            // Первые inputDelay тиков ни у кого приказов быть не может — они пустые без пересылки.
            return tick < firstTick + inputDelay || inputs[(int)side].ContainsKey(tick);
        }

        private void Schedule(Side side, int tick)
        {
            if (!inputs[(int)side].TryGetValue(tick, out List<ICommand> commands))
            {
                return;
            }

            inputs[(int)side].Remove(tick);
            foreach (ICommand command in commands)
            {
                // Приказывать можно только своим — чужое отбрасывается одинаково на обеих машинах.
                if (CommandOwnership.IsAllowed(match, command, side))
                {
                    match.Enqueue(tick, command);
                }
            }
        }

        private void CompareHashes(int tick)
        {
            if (!localHashes.TryGetValue(tick, out ulong local) || !remoteHashes.TryGetValue(tick, out ulong remote))
            {
                return;
            }

            localHashes.Remove(tick);
            remoteHashes.Remove(tick);
            if (local != remote && !IsDesynced)
            {
                IsDesynced = true;
                DesyncTick = tick;
                Desynced?.Invoke();
            }
        }

        public static byte[] EncodeInput(int tick, IReadOnlyList<ICommand> commands)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)NetMessageType.TickInput);
                writer.Write(tick);
                writer.Write((ushort)commands.Count);
                foreach (ICommand command in commands)
                {
                    CommandCodec.Write(writer, command);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        public static byte[] EncodeHash(int tick, ulong hash)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)NetMessageType.StateHash);
                writer.Write(tick);
                writer.Write(hash);
                writer.Flush();
                return stream.ToArray();
            }
        }
    }
}
