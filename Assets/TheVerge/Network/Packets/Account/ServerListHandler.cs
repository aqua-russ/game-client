using TheVerge.Core.Session;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Account
{
    /// <summary>
    /// 0xA8 Server List: шарды логин-сервера. Сервер: Projects/Server/Network/Packets/OutgoingAccountPackets.cs,
    /// SendAccountLoginAck — после длины байт 0x5D, ushort число шардов, на каждый 40 байт: ushort номер, имя WriteLatin1(32),
    /// byte заполненность, sbyte часовой пояс, uint RawAddress. RawAddress — байты IPv4, прочитанные как little-endian
    /// (ServerInfo.cs), а пишутся они big-endian, поэтому IP в пакете идёт задом наперёд: 127.0.0.1 → 01 00 00 7F.
    /// </summary>
    public sealed class ServerListHandler
    {
        public const byte PacketId = 0xA8;

        /// <summary>Байт 0x5D и ushort число шардов сразу после длины.</summary>
        private const int ListHeaderLength = 3;

        private const int NameLength = 32;

        /// <summary>Номер (2) + имя (32) + заполненность (1) + пояс (1) + IP (4).</summary>
        private const int EntryLength = 2 + NameLength + 1 + 1 + IPv4Text.Length;

        private readonly LoginFlow _flow;

        public ServerListHandler(LoginFlow flow)
        {
            _flow = flow;
        }

        public void Handle(ref PacketReader reader)
        {
            if (reader.Remaining < ListHeaderLength)
            {
                _flow.OnMalformedPacket("0xA8: нет числа шардов (" + reader.Remaining + " байт после длины)");
                return;
            }

            reader.Skip(1); // 0x5D — сервер всегда пишет это число, клиенту оно ничего не говорит
            var count = reader.ReadUInt16();
            if (reader.Remaining != count * EntryLength)
            {
                _flow.OnMalformedPacket("0xA8: шардов " + count + ", а данных " + reader.Remaining + " байт");
                return;
            }

            var shards = new ShardEntry[count];
            for (var i = 0; i < count; i++)
            {
                shards[i] = ReadEntry(ref reader);
            }

            _flow.OnShardList(shards);
        }

        private static ShardEntry ReadEntry(ref PacketReader reader)
        {
            var index = reader.ReadUInt16();
            var name = reader.ReadLatin1(NameLength);
            var percent = reader.ReadByte();
            var timeZone = reader.ReadSByte();
            var d = reader.ReadByte();
            var c = reader.ReadByte();
            var b = reader.ReadByte();
            var a = reader.ReadByte();
            return new ShardEntry(index, name, percent, timeZone, IPv4Text.Format(a, b, c, d));
        }
    }
}
