using TheVerge.Core.Primitives;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Items
{
    /// <summary>
    /// 0x06 Double Click: использовать предмет или моба (открыть контейнер). Сервер: Projects/UOContent/Network/Packets/
    /// IncomingEntityPackets.cs, UseReq — ReadUInt32 серийник; длина 5 (Register(0x06, 5, …)). Старший бит 0x80000000 —
    /// «кукла своего персонажа» (Стадия 5), здесь не ставится.
    /// </summary>
    public static class DoubleClickPacket
    {
        public const byte PacketId = 0x06;

        public static void Write(ref PacketWriter writer, Serial serial)
        {
            writer.Write(PacketId);
            writer.Write(serial.Value);
        }
    }
}
