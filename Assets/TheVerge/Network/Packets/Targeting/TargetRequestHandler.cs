using TheVerge.Core.Targeting;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Targeting
{
    /// <summary>
    /// 0x6C Target Request: сервер открыл или закрыл курсор. Сервер: Projects/Server/Network/Packets/OutgoingTargetPackets.cs,
    /// SendTargetReq — bool «можно ли клетку», int номер курсора, byte флаги, 12 нулей; SendCancelTarget — флаг 3.
    /// </summary>
    public sealed class TargetRequestHandler
    {
        public const byte PacketId = 0x6C;

        private readonly TargetCursor _cursor;

        public TargetRequestHandler(TargetCursor cursor)
        {
            _cursor = cursor;
        }

        public void Handle(ref PacketReader reader)
        {
            var allowsLocation = reader.ReadBoolean();
            var cursorId = reader.ReadInt32();
            var flags = reader.ReadByte();
            _cursor.OnRequest(allowsLocation, cursorId, flags);
        }
    }
}
