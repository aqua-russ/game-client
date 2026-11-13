using System;
using System.Buffers.Binary;
using System.Text;

namespace TheVerge.Tests.Network.Packets.Account
{
    /// <summary>
    /// Пакеты входа так, как их пишет сервер (Projects/Server/Network/Packets/OutgoingAccountPackets.cs), — повтор кода
    /// сервера по шагам, без кода клиента.
    /// </summary>
    internal static class AccountServerSamples
    {
        /// <summary>SendAccountLoginAck: 0xA8, ushort длина, 0x5D, ushort число, на шард 40 байт.</summary>
        public static byte[] ServerList(params (string Name, int Percent, int TimeZone, byte[] Ip)[] shards)
        {
            var length = 6 + 40 * shards.Length;
            var packet = new byte[length];
            packet[0] = 0xA8;
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(1), (ushort)length);
            packet[3] = 0x5D;
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(4), (ushort)shards.Length);
            for (var i = 0; i < shards.Length; i++)
            {
                var at = 6 + 40 * i;
                BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(at), (ushort)i);
                Encoding.Latin1.GetBytes(shards[i].Name).CopyTo(packet.AsSpan(at + 2, 32));
                packet[at + 34] = (byte)shards[i].Percent;
                packet[at + 35] = (byte)(sbyte)shards[i].TimeZone;
                // ServerInfo.RawAddress = ReadUInt32LittleEndian(байты IP); writer.Write(uint) — big-endian.
                var raw = BinaryPrimitives.ReadUInt32LittleEndian(shards[i].Ip);
                BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(at + 36), raw);
            }

            return packet;
        }

        /// <summary>SendPlayServerAck: 0x8C, WriteLE(RawAddress), short порт, int ключ.</summary>
        public static byte[] PlayServerAck(byte[] ip, int port, int authId)
        {
            var packet = new byte[11];
            packet[0] = 0x8C;
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(1), BinaryPrimitives.ReadUInt32LittleEndian(ip));
            BinaryPrimitives.WriteInt16BigEndian(packet.AsSpan(5), (short)port);
            BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(7), authId);
            return packet;
        }
    }
}
