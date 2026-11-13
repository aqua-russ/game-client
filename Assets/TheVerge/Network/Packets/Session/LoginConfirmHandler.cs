using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Session
{
    /// <summary>
    /// 0x1B Login Confirm: вход в мир подтверждён. Сервер: Projects/Server/Network/Packets/OutgoingAccountPackets.cs,
    /// SendLoginConfirmation — Serial, int 0, short тело, short X, Y, Z, byte направление, byte 0, int -1, int 0,
    /// short ширина и высота карты, дальше нули до 37 байт.
    /// </summary>
    public sealed class LoginConfirmHandler
    {
        public const byte PacketId = 0x1B;

        private readonly WorldEntry _world;

        public LoginConfirmHandler(WorldEntry world)
        {
            _world = world;
        }

        /// <summary>int 0 после серийника.</summary>
        private const int UnusedAfterSerial = 4;

        /// <summary>byte 0, int -1, int 0 после направления.</summary>
        private const int UnusedAfterDirection = 1 + 4 + 4;

        public void Handle(ref PacketReader reader)
        {
            var serial = new Serial(reader.ReadUInt32());
            reader.Skip(UnusedAfterSerial);
            var body = reader.ReadUInt16();
            var x = reader.ReadInt16();
            var y = reader.ReadInt16();
            var z = (sbyte)reader.ReadInt16(); // сервер пишет Z как short, в UO высота — sbyte
            var direction = (Direction)reader.ReadByte();
            reader.Skip(UnusedAfterDirection);
            var map = new MapSize(reader.ReadInt16(), reader.ReadInt16());
            _world.OnLoginConfirm(new PlayerState(serial, body, new Point3D(x, y, z), direction, map));
        }
    }
}
