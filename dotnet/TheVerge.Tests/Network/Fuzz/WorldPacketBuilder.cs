using System;
using System.Buffers.Binary;
using static TheVerge.Tests.Network.Fuzz.WorldFuzzTests;

namespace TheVerge.Tests.Network.Fuzz
{
    /// <summary>
    /// Пакеты мира правильного вида (как пишет сервер: OutgoingMobilePackets, OutgoingEntityPackets, OutgoingEquipmentPackets,
    /// OutgoingContainerPackets, OutgoingPlayerPackets) со случайными полями из небольших наборов (<see cref="Pool"/>).
    /// </summary>
    internal static class WorldPacketBuilder
    {
        public static int Build(Random r, byte[] b)
        {
            switch (r.Next(9))
            {
                case 0: return MobileIncoming(r, b);
                case 1: return Fixed(b, 0x77, w => { w.U32(Pool.Any(r).Value); w.U16(0x11); w.Cell(Pool.Cell(r)); w.U8(0); w.U16(0); w.U8(0); w.U8(3); });
                case 2: return Fixed(b, 0x2E, w => { w.U32(Pool.Any(r).Value); w.U16(0x1517); w.U16((ushort)r.Next(0, 0x22)); w.U32(Pool.Any(r).Value); w.U16(0); });
                case 3: return Fixed(b, 0x1D, w => w.U32(Pool.Any(r).Value));
                case 4: return WorldObject(r, b);
                case 5: return ContainerContent(r, b);
                case 6: return Fixed(b, 0x25, w => Entry(r, w));
                case 7: return Fixed(b, 0x24, w => { w.U32(Pool.Any(r).Value); w.U16(0x3C); w.U16(0x7D); });
                default: return Fixed(b, 0x76, w => { var c = Pool.Cell(r); w.U16((ushort)c.X); w.U16((ushort)c.Y); w.U16((ushort)c.Z); w.U8(0); w.U16(0); w.U16(0); w.U16(7168); w.U16(4096); });
            }
        }

        private static int MobileIncoming(Random r, byte[] b)
        {
            var w = new Writer(b, 3);
            w.U32(Pool.Any(r).Value);
            w.U16(0x190);
            w.Cell(Pool.Cell(r));
            w.U8(0);
            w.U16(0);
            w.U8(0);
            w.U8(1);
            for (var i = r.Next(0, 6); i > 0; i--)
            {
                w.U32(Pool.Any(r).Value | 1); // ноль — конец списка: внутри списка его не бывает
                w.U16(0x1517);
                w.U8((byte)r.Next(0, 0x22));
                w.U16(0);
            }

            w.U32(0);
            return Variable(b, 0x78, w.Position);
        }

        private static int WorldObject(Random r, byte[] b) => Fixed(b, 0xF3, w =>
        {
            w.U16(1);
            w.U8((byte)r.Next(0, 4));
            w.U32(Pool.Any(r).Value);
            w.U16(0x0E75);
            w.U8(0);
            w.U16(1);
            w.U16(1);
            w.Cell(Pool.Cell(r));
            w.U8(0);
            w.U16(0);
            w.U8(0);
            w.U16(0);
        });

        private static int ContainerContent(Random r, byte[] b)
        {
            var w = new Writer(b, 3);
            var count = r.Next(0, 6);
            w.U16((ushort)count);
            for (var i = 0; i < count; i++)
            {
                Entry(r, w);
            }

            return Variable(b, 0x3C, w.Position);
        }

        private static void Entry(Random r, Writer w)
        {
            w.U32(Pool.Any(r).Value);
            w.U16(0x09D0);
            w.U8(0);
            w.U16((ushort)r.Next(1, 100));
            w.U16((ushort)r.Next(0, 200));
            w.U16((ushort)r.Next(0, 200));
            w.U8(0);
            w.U32(r.Next(5) == 0 ? Pool.Any(r).Value : Pool.Item(r).Value);
            w.U16(0);
        }

        private static int Fixed(byte[] b, byte id, Action<Writer> body)
        {
            b[0] = id;
            var w = new Writer(b, 1);
            body(w);
            return w.Position;
        }

        private static int Variable(byte[] b, byte id, int length)
        {
            b[0] = id;
            BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(1), (ushort)length);
            return length;
        }

        private sealed class Writer
        {
            private readonly byte[] _b;

            public Writer(byte[] b, int position)
            {
                _b = b;
                Position = position;
            }

            public int Position { get; private set; }

            public void U8(byte v) => _b[Position++] = v;

            public void U16(ushort v)
            {
                BinaryPrimitives.WriteUInt16BigEndian(_b.AsSpan(Position), v);
                Position += 2;
            }

            public void U32(uint v)
            {
                BinaryPrimitives.WriteUInt32BigEndian(_b.AsSpan(Position), v);
                Position += 4;
            }

            public void Cell(TheVerge.Core.Primitives.Point3D c)
            {
                U16((ushort)c.X);
                U16((ushort)c.Y);
                U8((byte)c.Z);
            }
        }
    }
}
