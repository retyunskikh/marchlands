using System;
using System.IO;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Commands
{
    /// <summary>
    /// Команды в байты и обратно — для сетевой игры: по сети идут только приказы, симуляцию каждая сторона
    /// считает сама. float пишется побитно, поэтому прочитанная команда в точности равна отправленной.
    /// </summary>
    public static class CommandCodec
    {
        private enum Tag : byte
        {
            Move = 1,
            Face,
            Stop,
            Volley,
            Charge,
            Target,
            Assault,
            ChangeFormation,
            SetShotMode,
            BuildBarracks,
            BuildStable,
            BuildDepot,
            BuildHouse,
            SetHiring,
            FormSquad,
            DisbandReserve,
            OrderGun,
            UpgradeBuilding,
            ResearchBalloon
        }

        public static void Write(BinaryWriter writer, ICommand command)
        {
            switch (command)
            {
                case MoveCommand move:
                    writer.Write((byte)Tag.Move);
                    writer.Write(move.SquadId);
                    Write(writer, move.Target);
                    writer.Write(move.FinalFacing);
                    writer.Write(move.AutoRoute);
                    break;
                case FaceCommand face:
                    writer.Write((byte)Tag.Face);
                    writer.Write(face.SquadId);
                    writer.Write(face.Facing);
                    break;
                case StopCommand stop:
                    writer.Write((byte)Tag.Stop);
                    writer.Write(stop.SquadId);
                    break;
                case VolleyCommand volley:
                    writer.Write((byte)Tag.Volley);
                    writer.Write(volley.SquadId);
                    writer.Write(volley.MaxDistance);
                    break;
                case ChargeCommand charge:
                    writer.Write((byte)Tag.Charge);
                    writer.Write(charge.SquadId);
                    writer.Write(charge.TargetSquadId);
                    break;
                case TargetCommand target:
                    writer.Write((byte)Tag.Target);
                    writer.Write(target.SquadId);
                    writer.Write(target.TargetSquadId);
                    break;
                case AssaultCommand assault:
                    writer.Write((byte)Tag.Assault);
                    writer.Write(assault.SquadId);
                    writer.Write(assault.TargetBuildingId);
                    break;
                case ChangeFormationCommand formation:
                    writer.Write((byte)Tag.ChangeFormation);
                    writer.Write(formation.SquadId);
                    writer.Write((byte)formation.Formation);
                    break;
                case SetShotModeCommand shotMode:
                    writer.Write((byte)Tag.SetShotMode);
                    writer.Write(shotMode.SquadId);
                    writer.Write((byte)shotMode.Mode);
                    break;
                case BuildBarracksCommand barracks:
                    writer.Write((byte)Tag.BuildBarracks);
                    writer.Write((byte)barracks.Side);
                    Write(writer, barracks.Position);
                    break;
                case BuildStableCommand stable:
                    writer.Write((byte)Tag.BuildStable);
                    writer.Write((byte)stable.Side);
                    Write(writer, stable.Position);
                    break;
                case BuildDepotCommand depot:
                    writer.Write((byte)Tag.BuildDepot);
                    writer.Write((byte)depot.Side);
                    Write(writer, depot.Position);
                    break;
                case BuildHouseCommand house:
                    writer.Write((byte)Tag.BuildHouse);
                    writer.Write((byte)house.Side);
                    break;
                case SetHiringCommand hiring:
                    writer.Write((byte)Tag.SetHiring);
                    writer.Write(hiring.BuildingId);
                    writer.Write((byte)hiring.Type);
                    writer.Write(hiring.Hiring);
                    break;
                case FormSquadCommand form:
                    writer.Write((byte)Tag.FormSquad);
                    writer.Write((byte)form.Side);
                    writer.Write((byte)form.Type);
                    break;
                case DisbandReserveCommand disband:
                    writer.Write((byte)Tag.DisbandReserve);
                    writer.Write((byte)disband.Side);
                    writer.Write((byte)disband.Type);
                    break;
                case OrderGunCommand gun:
                    writer.Write((byte)Tag.OrderGun);
                    writer.Write(gun.BuildingId);
                    break;
                case UpgradeBuildingCommand upgrade:
                    writer.Write((byte)Tag.UpgradeBuilding);
                    writer.Write(upgrade.BuildingId);
                    break;
                case ResearchBalloonCommand balloon:
                    writer.Write((byte)Tag.ResearchBalloon);
                    writer.Write((byte)balloon.Side);
                    break;
                default:
                    throw new NotSupportedException($"Команду {command?.GetType().Name ?? "null"} нельзя передать по сети.");
            }
        }

        /// <summary>Читает команду; на испорченных данных — <see cref="InvalidDataException"/>.</summary>
        public static ICommand Read(BinaryReader reader)
        {
            var tag = (Tag)reader.ReadByte();
            switch (tag)
            {
                case Tag.Move:
                    return new MoveCommand(reader.ReadInt32(), ReadFloat2(reader), reader.ReadSingle(), reader.ReadBoolean());
                case Tag.Face:
                    return new FaceCommand(reader.ReadInt32(), reader.ReadSingle());
                case Tag.Stop:
                    return new StopCommand(reader.ReadInt32());
                case Tag.Volley:
                    return new VolleyCommand(reader.ReadInt32(), reader.ReadSingle());
                case Tag.Charge:
                    return new ChargeCommand(reader.ReadInt32(), reader.ReadInt32());
                case Tag.Target:
                    return new TargetCommand(reader.ReadInt32(), reader.ReadInt32());
                case Tag.Assault:
                    return new AssaultCommand(reader.ReadInt32(), reader.ReadInt32());
                case Tag.ChangeFormation:
                    return new ChangeFormationCommand(reader.ReadInt32(), ReadEnum<Formation>(reader));
                case Tag.SetShotMode:
                    return new SetShotModeCommand(reader.ReadInt32(), ReadEnum<ShotMode>(reader));
                case Tag.BuildBarracks:
                    return new BuildBarracksCommand(ReadSide(reader), ReadFloat2(reader));
                case Tag.BuildStable:
                    return new BuildStableCommand(ReadSide(reader), ReadFloat2(reader));
                case Tag.BuildDepot:
                    return new BuildDepotCommand(ReadSide(reader), ReadFloat2(reader));
                case Tag.BuildHouse:
                    return new BuildHouseCommand(ReadSide(reader));
                case Tag.SetHiring:
                    return new SetHiringCommand(reader.ReadInt32(), ReadEnum<UnitType>(reader), reader.ReadBoolean());
                case Tag.FormSquad:
                    return new FormSquadCommand(ReadSide(reader), ReadEnum<UnitType>(reader));
                case Tag.DisbandReserve:
                    return new DisbandReserveCommand(ReadSide(reader), ReadEnum<UnitType>(reader));
                case Tag.OrderGun:
                    return new OrderGunCommand(reader.ReadInt32());
                case Tag.UpgradeBuilding:
                    return new UpgradeBuildingCommand(reader.ReadInt32());
                case Tag.ResearchBalloon:
                    return new ResearchBalloonCommand(ReadSide(reader));
                default:
                    throw new InvalidDataException($"Неизвестная команда {(byte)tag}.");
            }
        }

        public static byte[] ToBytes(ICommand command)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                Write(writer, command);
                writer.Flush();
                return stream.ToArray();
            }
        }

        public static ICommand FromBytes(byte[] bytes)
        {
            using (var reader = new BinaryReader(new MemoryStream(bytes)))
            {
                return Read(reader);
            }
        }

        private static void Write(BinaryWriter writer, float2 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
        }

        private static float2 ReadFloat2(BinaryReader reader)
        {
            float x = reader.ReadSingle();
            return new float2(x, reader.ReadSingle());
        }

        private static Side ReadSide(BinaryReader reader)
        {
            return ReadEnum<Side>(reader);
        }

        private static T ReadEnum<T>(BinaryReader reader) where T : Enum
        {
            byte value = reader.ReadByte();
            if (!Enum.IsDefined(typeof(T), (int)value))
            {
                throw new InvalidDataException($"{typeof(T).Name}: недопустимое значение {value}.");
            }

            return (T)Enum.ToObject(typeof(T), value);
        }
    }
}
