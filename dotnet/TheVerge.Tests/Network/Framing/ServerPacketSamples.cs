using System.Collections.Generic;
using TheVerge.Network.IO;

namespace TheVerge.Tests.Network.Framing
{
    /// <summary>
    /// Пакеты сервер → клиент, собранные байт в байт так, как их собирает сервер (метод сервера — в комментарии).
    /// Значения полей произвольные: нарезке важны только номер, длина и подкоманда.
    /// </summary>
    internal static class ServerPacketSamples
    {
        /// <summary>Поток входа и первых пакетов в мире — все виды заголовков: постоянная длина, 2 байта длины, подкоманды.</summary>
        public static IReadOnlyList<byte[]> LoginStream() => new[]
        {
            LoginRejected(),
            ServerList(),
            ServerRedirect(),
            SupportedFeatures(),
            LoginConfirm(),
            MapChange(),
            ClientVersionRequest(),
            GlobalLight(),
            PartyLocations(),
            LoginComplete(),
            PingAck(),
        };

        /// <summary>0x82: OutgoingAccountPackets.cs, SendAccountLoginRejected — номер и причина.</summary>
        public static byte[] LoginRejected() => new byte[] { 0x82, 0x03 };

        /// <summary>0xA8: OutgoingAccountPackets.cs, SendAccountLoginAck — длина 6 + 40 на шард.</summary>
        public static byte[] ServerList()
        {
            var packet = new byte[46];
            var writer = new PacketWriter(packet);
            writer.Write((byte)0xA8);
            writer.Write((ushort)46);
            writer.Write((byte)0x5D);
            writer.Write((ushort)1);
            writer.Write((ushort)0);
            writer.WriteLatin1("The Verge", 32);
            writer.Write((byte)0);
            writer.Write((sbyte)0);
            writer.Write(0x0100007Fu);
            return packet;
        }

        /// <summary>0x8C: OutgoingAccountPackets.cs, SendPlayServerAck — адрес WriteLE, порт, ключ.</summary>
        public static byte[] ServerRedirect() => new byte[] { 0x8C, 127, 0, 0, 1, 0x0A, 0x21, 0x12, 0x34, 0x56, 0x78 };

        /// <summary>0xB9: OutgoingAccountPackets.cs, SendSupportedFeature — для 7.0.95.0 флаги uint (5 байт).</summary>
        public static byte[] SupportedFeatures() => new byte[] { 0xB9, 0x00, 0x00, 0x80, 0x3F };

        /// <summary>0x1B: OutgoingAccountPackets.cs, SendLoginConfirmation — 37 байт.</summary>
        public static byte[] LoginConfirm()
        {
            var packet = new byte[37];
            var writer = new PacketWriter(packet);
            writer.Write((byte)0x1B);
            writer.Write(0x00000042u);
            writer.Write(0);
            writer.Write((short)0x0190);
            writer.Write((short)2500);
            writer.Write((short)500);
            writer.Write((short)0);
            writer.Write((byte)2);
            writer.Write((byte)0);
            writer.Write(-1);
            writer.Write(0);
            writer.Write((short)7168);
            writer.Write((short)4096);
            writer.WriteZeros(writer.Remaining);
            return packet;
        }

        /// <summary>0xBF 0x08: OutgoingMapPackets.cs, SendMapChange — 6 байт, подкоманда 2 байта после длины.</summary>
        public static byte[] MapChange() => new byte[] { 0xBF, 0x00, 0x06, 0x00, 0x08, 0x00 };

        /// <summary>0xBD: OutgoingAccountPackets.cs, SendClientVersionRequest — только заголовок.</summary>
        public static byte[] ClientVersionRequest() => new byte[] { 0xBD, 0x00, 0x03 };

        /// <summary>0x4F: OutgoingLightPackets.cs, SendGlobalLightLevel.</summary>
        public static byte[] GlobalLight() => new byte[] { 0x4F, 0x05 };

        /// <summary>0xF0 0x01: UOContent/Network/MapUO.cs, SendPartyMemberLocations — подкоманда 1 байт, в конце int 0.</summary>
        public static byte[] PartyLocations()
        {
            var packet = new byte[17];
            var writer = new PacketWriter(packet);
            writer.Write((byte)0xF0);
            writer.Write((ushort)17);
            writer.Write((byte)0x01);
            writer.Write(0x00000043u);
            writer.Write((short)1000);
            writer.Write((short)2000);
            writer.Write((byte)0);
            writer.Write(0);
            return packet;
        }

        /// <summary>0x55: OutgoingAccountPackets.cs, SendLoginComplete.</summary>
        public static byte[] LoginComplete() => new byte[] { 0x55 };

        /// <summary>0x73: OutgoingPlayerPackets.cs, SendPingAck.</summary>
        public static byte[] PingAck() => new byte[] { 0x73, 0x07 };

        public static byte[] Concat(IReadOnlyList<byte[]> packets)
        {
            var total = new List<byte>();
            foreach (var packet in packets)
            {
                total.AddRange(packet);
            }

            return total.ToArray();
        }
    }
}
