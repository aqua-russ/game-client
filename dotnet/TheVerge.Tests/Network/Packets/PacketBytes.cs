using System;
using TheVerge.Network.IO;

namespace TheVerge.Tests.Network.Packets
{
    /// <summary>Записать пакет через PacketWriter и получить его байты (PacketWriter — ref struct, поэтому через делегат с ref).</summary>
    internal static class PacketBytes
    {
        public delegate void WriteAction(ref PacketWriter writer);

        public static byte[] Write(WriteAction write)
        {
            var buffer = new byte[1024];
            var writer = new PacketWriter(buffer);
            write(ref writer);
            return buffer.AsSpan(0, writer.Position).ToArray();
        }
    }
}
