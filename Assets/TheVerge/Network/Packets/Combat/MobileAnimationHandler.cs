using TheVerge.Core.Combat;
using TheVerge.Core.Primitives;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Combat
{
    /// <summary>
    /// 0x6E Mobile Animation: моб, short действие, кадров, повторов, bool «назад», повторять, byte задержка. Сервер:
    /// Projects/Server/Network/Packets/OutgoingMobilePackets.cs, CreateMobileAnimation (Mobile.Animate — всем рядом).
    /// В журнал — моб и действие; кадры и повторы понадобятся для показа (Стадия 9).
    /// </summary>
    public sealed class MobileAnimationHandler
    {
        public const byte PacketId = 0x6E;

        private readonly CombatLog _log;

        public MobileAnimationHandler(CombatLog log)
        {
            _log = log;
        }

        public void Handle(ref PacketReader reader)
        {
            var mobile = new Serial(reader.ReadUInt32());
            int action = reader.ReadInt16();
            reader.Skip(7);
            _log.OnAnimation(mobile, action);
        }
    }
}
