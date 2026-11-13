using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace TheVerge.Tests.Network.Packets.World
{
    /// <summary>
    /// Пакеты мира так, как их пишет сервер для клиента 7.0.95.0 (Projects/Server/Network/Packets): поле за полем, в том
    /// же порядке и тех же размерах, что SpanWriter в методах, указанных у каждого пакета.
    /// </summary>
    internal static class ServerWorldPackets
    {
        public readonly struct Equipped
        {
            public Equipped(uint serial, ushort itemId, byte layer, ushort hue)
            {
                Serial = serial;
                ItemId = itemId;
                Layer = layer;
                Hue = hue;
            }

            public uint Serial { get; }
            public ushort ItemId { get; }
            public byte Layer { get; }
            public ushort Hue { get; }
        }

        /// <summary>Поля моба, общие для 0x78, 0x77 и 0x20.</summary>
        public sealed class Mob
        {
            public uint Serial { get; set; }
            public ushort Body { get; set; } = 0x0190;
            public short X { get; set; }
            public short Y { get; set; }
            public sbyte Z { get; set; }
            public byte Direction { get; set; }
            public ushort Hue { get; set; }
            public byte Flags { get; set; }
            public byte Notoriety { get; set; } = 1;
        }

        /// <summary>OutgoingMobilePackets.SendMobileIncoming, новый формат (7.0.33.1+): цвет у каждого предмета, в конце int 0.</summary>
        public static byte[] MobileIncoming(Mob m, params Equipped[] items)
        {
            var p = new List<byte> { 0x78, 0, 0 };
            U32(p, m.Serial);
            U16(p, m.Body);
            U16(p, (ushort)m.X);
            U16(p, (ushort)m.Y);
            p.Add((byte)m.Z);
            p.Add(m.Direction);
            U16(p, m.Hue);
            p.Add(m.Flags);
            p.Add(m.Notoriety);
            foreach (var item in items)
            {
                U32(p, item.Serial);
                U16(p, item.ItemId);
                p.Add(item.Layer);
                U16(p, item.Hue);
            }

            U32(p, 0);
            var bytes = p.ToArray();
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(1), (ushort)bytes.Length);
            return bytes;
        }

        /// <summary>OutgoingMobilePackets.SendMobileUpdate: Serial, тело, 0, цвет, флаги, X, Y, short 0, направление, Z.</summary>
        public static byte[] MobileUpdate(Mob m)
        {
            var p = new List<byte> { 0x20 };
            U32(p, m.Serial);
            U16(p, m.Body);
            p.Add(0);
            U16(p, m.Hue);
            p.Add(m.Flags);
            U16(p, (ushort)m.X);
            U16(p, (ushort)m.Y);
            U16(p, 0);
            p.Add(m.Direction);
            p.Add((byte)m.Z);
            return p.ToArray();
        }

        /// <summary>OutgoingMobilePackets.CreateMobileMoving: Serial, тело, X, Y, Z, направление, цвет, флаги, «злость».</summary>
        public static byte[] MobileMoving(Mob m)
        {
            var p = new List<byte> { 0x77 };
            U32(p, m.Serial);
            U16(p, m.Body);
            U16(p, (ushort)m.X);
            U16(p, (ushort)m.Y);
            p.Add((byte)m.Z);
            p.Add(m.Direction);
            U16(p, m.Hue);
            p.Add(m.Flags);
            p.Add(m.Notoriety);
            return p.ToArray();
        }

        /// <summary>
        /// OutgoingEquipmentPackets.SendEquipUpdate: Serial, (short)ItemID, (ushort)Layer — байт 0 и слой, Serial моба,
        /// цвет. Волосы — OutgoingVirtualHairPackets.CreateHairEquipUpdatePacket, те же байты.
        /// </summary>
        public static byte[] EquipUpdate(uint serial, ushort itemId, byte layer, uint parent, ushort hue)
        {
            var p = new List<byte> { 0x2E };
            U32(p, serial);
            U16(p, itemId);
            U16(p, layer);
            U32(p, parent);
            U16(p, hue);
            return p.ToArray();
        }

        /// <summary>OutgoingEntityPackets.CreateRemoveEntity.</summary>
        public static byte[] RemoveEntity(uint serial)
        {
            var p = new List<byte> { 0x1D };
            U32(p, serial);
            return p.ToArray();
        }

        /// <summary>Поля предмета для 0xF3.</summary>
        public sealed class WorldEntity
        {
            public byte Type { get; set; }
            public uint Serial { get; set; }
            public ushort Graphic { get; set; }
            public short Amount { get; set; } = 1;
            public short X { get; set; }
            public short Y { get; set; }
            public sbyte Z { get; set; }
            public byte Light { get; set; }
            public ushort Hue { get; set; }
            public byte Flags { get; set; }
        }

        /// <summary>
        /// OutgoingEntityPackets.CreateWorldEntity для клиента High Seas (7.0.9.0+): 0xF3, short 1, byte тип (0 предмет,
        /// 1 моб, 2 multi), Serial, ushort графика, byte 0, short количество дважды, short X &amp; 0x7FFF, Y &amp; 0x3FFF, sbyte Z,
        /// byte свет, short цвет, byte флаги, short 0.
        /// </summary>
        public static byte[] WorldObject(WorldEntity e)
        {
            var p = new List<byte> { 0xF3 };
            U16(p, 1);
            p.Add(e.Type);
            U32(p, e.Serial);
            U16(p, e.Graphic);
            p.Add(0);
            U16(p, (ushort)e.Amount);
            U16(p, (ushort)e.Amount);
            U16(p, (ushort)(e.X & 0x7FFF));
            U16(p, (ushort)(e.Y & 0x3FFF));
            p.Add((byte)e.Z);
            p.Add(e.Light);
            U16(p, e.Hue);
            p.Add(e.Flags);
            U16(p, 0);
            return p.ToArray();
        }

        /// <summary>OutgoingMapPackets.SendMapChange: 0xBF, длина 6, подкоманда 8, номер карты.</summary>
        public static byte[] MapChange(byte map) => new byte[] { 0xBF, 0x00, 0x06, 0x00, 0x08, map };

        /// <summary>OutgoingMapPackets.SendMapPatches: 0xBF, длина 41, подкоманда 0x18, int 4, на каждую карту int статика, int земля.</summary>
        public static byte[] MapPatches(params int[] staticAndLand)
        {
            var p = new List<byte> { 0xBF };
            U16(p, (ushort)(9 + staticAndLand.Length * 4));
            U16(p, 0x18);
            U32(p, (uint)(staticAndLand.Length / 2));
            foreach (var value in staticAndLand)
            {
                U32(p, (uint)value);
            }

            return p.ToArray();
        }

        /// <summary>OutgoingPlayerPackets.SendServerChange: X, Y, Z (short), byte 0, short 0, short 0, ширина, высота карты.</summary>
        public static byte[] ServerChange(short x, short y, short z, short width, short height)
        {
            var p = new List<byte> { 0x76 };
            U16(p, (ushort)x);
            U16(p, (ushort)y);
            U16(p, (ushort)z);
            p.Add(0);
            U16(p, 0);
            U16(p, 0);
            U16(p, (ushort)width);
            U16(p, (ushort)height);
            return p.ToArray();
        }

        /// <summary>OutgoingEffectPackets.CreateSoundEffect: byte 1, short звук, short 0 (громкость), short X, Y, Z.</summary>
        public static byte[] PlaySound(ushort sound, short x, short y, short z)
        {
            var p = new List<byte> { 0x54, 1 };
            U16(p, sound);
            U16(p, 0);
            U16(p, (ushort)x);
            U16(p, (ushort)y);
            U16(p, (ushort)z);
            return p.ToArray();
        }

        private static void U16(List<byte> p, ushort value)
        {
            p.Add((byte)(value >> 8));
            p.Add((byte)value);
        }

        private static void U32(List<byte> p, uint value)
        {
            U16(p, (ushort)(value >> 16));
            U16(p, (ushort)value);
        }
    }
}
