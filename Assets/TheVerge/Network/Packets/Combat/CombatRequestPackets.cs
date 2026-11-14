using TheVerge.Core.Primitives;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Combat
{
    /// <summary>
    /// Боевые запросы клиента — как читает сервер (Projects/UOContent/Network/Packets/IncomingPlayerPackets.cs): 0x72
    /// SetWarMode — bool (дальше сервер не читает; хвост 0x00 0x32 0x00 — как в его собственном ответе), 0x05 AttackReq —
    /// uint серийник. Оба по 5 байт.
    /// </summary>
    public static class CombatRequestPackets
    {
        public const byte WarModeId = 0x72;
        public const byte AttackId = 0x05;
        public const int Length = 5;

        public static void WriteWarMode(ref PacketWriter writer, bool warMode)
        {
            writer.Write(WarModeId);
            writer.Write(warMode);
            writer.Write((byte)0x00);
            writer.Write((byte)0x32);
            writer.Write((byte)0x00);
        }

        public static void WriteAttack(ref PacketWriter writer, Serial target)
        {
            writer.Write(AttackId);
            writer.Write(target.Value);
        }
    }
}
