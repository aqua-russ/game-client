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

        /// <summary>SendSupportedFeature для 6.0.14.2+ (ExtendedSupportedFeatures): 0xB9 и uint флаги.</summary>
        public static byte[] SupportedFeatures(uint flags)
        {
            var packet = new byte[5];
            packet[0] = 0xB9;
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(1), flags);
            return packet;
        }

        /// <summary>
        /// SendCharacterList для 7.0.13.0+ (NewCharacterList, textLength 32): число слотов, на слот имя UTF-8 30 байт и
        /// 30 нулей пароля; число городов, на город: byte номер, Latin-1 32 город, Latin-1 32 здание, int X, Y, Z, карта,
        /// описание, 0; int флаги; short -1. null в именах — пустой слот (writer.Clear(60)).
        /// </summary>
        public static byte[] CharacterList(string?[] names, City[] cities, int flags)
        {
            var length = 11 + (32 * 2 + 25) * cities.Length + names.Length * 60;
            var packet = new byte[length];
            packet[0] = 0xA9;
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(1), (ushort)length);
            packet[3] = (byte)names.Length;
            var at = 4;
            foreach (var name in names)
            {
                if (name != null)
                {
                    Encoding.UTF8.GetBytes(name).CopyTo(packet.AsSpan(at, 30));
                }

                at += 60;
            }

            packet[at++] = (byte)cities.Length;
            for (var i = 0; i < cities.Length; i++)
            {
                var c = cities[i];
                packet[at++] = (byte)i;
                Encoding.Latin1.GetBytes(c.Name).CopyTo(packet.AsSpan(at, 32));
                Encoding.Latin1.GetBytes(c.Building).CopyTo(packet.AsSpan(at + 32, 32));
                at += 64;
                foreach (var value in new[] { c.X, c.Y, c.Z, 0, c.Description, 0 }) // карта 0 — Фелукка
                {
                    BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(at), value);
                    at += 4;
                }
            }

            BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(at), flags);
            BinaryPrimitives.WriteInt16BigEndian(packet.AsSpan(at + 4), -1);
            return packet;
        }

        /// <summary>Город для SendCharacterList (CityInfo сервера) на карте 0 — Фелукке.</summary>
        public readonly struct City
        {
            public City(string name, string building, int x, int y, int z, int description)
            {
                Name = name;
                Building = building;
                X = x;
                Y = y;
                Z = z;
                Description = description;
            }

            public string Name { get; }

            public string Building { get; }

            public int X { get; }

            public int Y { get; }

            public int Z { get; }

            public int Description { get; }
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
