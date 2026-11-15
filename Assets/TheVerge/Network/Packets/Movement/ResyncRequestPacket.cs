using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Movement
{
    /// <summary>
    /// 0x22 Resynchronize от клиента (не то же, что подтверждение шага 0x22 от сервера): просьба прислать своё место заново.
    /// Сервер: Projects/UOContent/Network/Packets/IncomingPlayerPackets.cs, Resynchronize — длина 3, полей не читает; в ответ
    /// шлёт 0x20 и 0x78 своего персонажа, всё вокруг и сбрасывает счёт шагов (NetState.ResetMovementState).
    /// </summary>
    public static class ResyncRequestPacket
    {
        public const byte PacketId = 0x22;

        /// <summary>Два байта после номера: сервер их не читает.</summary>
        private const int UnreadBytes = 2;

        public static void Write(ref PacketWriter writer)
        {
            writer.Write(PacketId);
            writer.WriteZeros(UnreadBytes);
        }
    }
}
