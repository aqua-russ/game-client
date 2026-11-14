using TheVerge.Core.Primitives;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Status
{
    /// <summary>
    /// 0x34 Mobile Query, тип 4 — статус: int 0xEDEDEDED, byte тип, uint серийник (10 байт). Сервер:
    /// Projects/UOContent/Network/Packets/IncomingPlayerPackets.cs, MobileQuery → Mobile.OnStatsQuery (ответ — 0x11,
    /// если моб в дальности и виден; своему ещё замки статов).
    /// </summary>
    public static class StatusQueryPacket
    {
        public const byte PacketId = 0x34;
        public const byte StatusType = 0x04;
        private const uint Filler = 0xEDEDEDED;

        public static void Write(ref PacketWriter writer, Serial serial)
        {
            writer.Write(PacketId);
            writer.Write(Filler);
            writer.Write(StatusType);
            writer.Write(serial.Value);
        }
    }
}
