using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.World
{
    /// <summary>
    /// 0x76 Server Change: свой персонаж на новом месте при смене карты и размер карты. Сервер: Projects/Server/Network/
    /// Packets/OutgoingPlayerPackets.cs, SendServerChange — short X, Y, Z, byte 0, short 0, short 0, short ширина, высота.
    /// </summary>
    public sealed class ServerChangeHandler
    {
        public const byte PacketId = 0x76;

        private const int UnusedLength = 5;

        private readonly EntityStore _store;
        private readonly WorldEnvironment _environment;

        public ServerChangeHandler(EntityStore store, WorldEnvironment environment)
        {
            _store = store;
            _environment = environment;
        }

        public void Handle(ref PacketReader reader)
        {
            var x = reader.ReadInt16();
            var y = reader.ReadInt16();
            var z = (sbyte)reader.ReadInt16(); // сервер пишет Z как short, в UO высота — sbyte
            reader.Skip(UnusedLength);
            var size = new MapSize(reader.ReadInt16(), reader.ReadInt16());
            _store.MovePlayerTo(new Point3D(x, y, z));
            _environment.SetMapSize(size);
        }
    }
}
