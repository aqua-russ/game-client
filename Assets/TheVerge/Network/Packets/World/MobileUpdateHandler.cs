using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.World
{
    /// <summary>
    /// 0x20 Mobile Update: свой персонаж (сервер шлёт его только владельцу: телепорт, смена тела, цвета). Сервер:
    /// Projects/Server/Network/Packets/OutgoingMobilePackets.cs, SendMobileUpdate — Serial, short тело, byte 0, short цвет,
    /// byte флаги, short X, Y, short 0, byte направление, sbyte Z. «Злости» нет — прежняя остаётся. Шлёт его сервер, сбросив
    /// счёт шагов (NetState.ResetMovementState: Mobile.SetLocation при телепорте, Mobile.ProcessDelta при смене тела, цвета,
    /// направления, ресинхронизация, вход, смена карты), поэтому движение своего персонажа встаёт на эту клетку.
    /// </summary>
    public sealed class MobileUpdateHandler
    {
        public const byte PacketId = 0x20;

        private readonly EntityStore _store;
        private readonly MovementReplies? _movement;

        public MobileUpdateHandler(EntityStore store, MovementReplies? movement = null)
        {
            _store = store;
            _movement = movement;
        }

        public void Handle(ref PacketReader reader)
        {
            var serial = new Serial(reader.ReadUInt32());
            var body = reader.ReadUInt16();
            reader.Skip(1);
            var hue = reader.ReadUInt16();
            var flags = reader.ReadByte();
            var x = reader.ReadInt16();
            var y = reader.ReadInt16();
            reader.Skip(2);
            var direction = (Direction)reader.ReadByte();
            var z = reader.ReadSByte();
            var info = new MobileInfo(body, new Point3D(x, y, z), direction, hue, flags, MobileInfo.NotorietyUnknown);
            _store.UpdateMobile(serial, info);
            if (serial == _store.PlayerSerial)
            {
                _movement?.OnServerPosition(info.Position, direction);
            }
        }
    }
}
