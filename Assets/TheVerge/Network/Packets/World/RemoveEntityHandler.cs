using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.World
{
    /// <summary>
    /// 0x1D Remove Entity: объект больше не виден (удалён, ушёл, скрылся). Сервер: Projects/Server/Network/Packets/
    /// OutgoingEntityPackets.cs, CreateRemoveEntity — Serial. Вложенное и надетое пропадает вместе с ним.
    /// </summary>
    public sealed class RemoveEntityHandler
    {
        public const byte PacketId = 0x1D;

        private readonly EntityStore _store;

        public RemoveEntityHandler(EntityStore store)
        {
            _store = store;
        }

        public void Handle(ref PacketReader reader) => _store.Remove(new Serial(reader.ReadUInt32()));
    }
}
