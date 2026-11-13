using System;
using System.Buffers.Binary;

namespace TheVerge.Tests.Network.Packets.Session
{
    /// <summary>Пакеты входа в мир так, как их пишет сервер (OutgoingAccountPackets.cs).</summary>
    internal static class WorldSamples
    {
        /// <summary>SendLoginConfirmation: 0x1B, Serial, 0, тело 0x190, X, Y, Z, направление 0, 0, -1, 0, 7168, 4096, нули.</summary>
        public static byte[] LoginConfirm(uint serial, short x, short y, short z)
        {
            var p = new byte[37];
            p[0] = 0x1B;
            BinaryPrimitives.WriteUInt32BigEndian(p.AsSpan(1), serial);
            BinaryPrimitives.WriteInt16BigEndian(p.AsSpan(9), 0x190);
            BinaryPrimitives.WriteInt16BigEndian(p.AsSpan(11), x);
            BinaryPrimitives.WriteInt16BigEndian(p.AsSpan(13), y);
            BinaryPrimitives.WriteInt16BigEndian(p.AsSpan(15), z);
            BinaryPrimitives.WriteInt32BigEndian(p.AsSpan(19), -1);
            BinaryPrimitives.WriteInt16BigEndian(p.AsSpan(27), 7168);
            BinaryPrimitives.WriteInt16BigEndian(p.AsSpan(29), 4096);
            return p;
        }
    }
}
