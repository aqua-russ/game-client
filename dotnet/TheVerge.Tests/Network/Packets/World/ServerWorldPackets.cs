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
