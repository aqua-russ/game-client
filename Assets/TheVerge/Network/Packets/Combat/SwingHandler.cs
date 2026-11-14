using TheVerge.Core.Combat;
using TheVerge.Core.Primitives;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Combat
{
    /// <summary>
    /// 0x2F Swing: byte 0, атакующий, цель. Сервер: Projects/Server/Network/Packets/OutgoingCombatPackets.cs, SendSwing;
    /// шлёт только атакующему (BaseWeapon.OnSwing: attacker.NetState?.SendSwing) — замахи чужих по нам не приходят.
    /// </summary>
    public sealed class SwingHandler
    {
        public const byte PacketId = 0x2F;

        private readonly CombatLog _log;

        public SwingHandler(CombatLog log)
        {
            _log = log;
        }

        public void Handle(ref PacketReader reader)
        {
            reader.Skip(1);
            var attacker = new Serial(reader.ReadUInt32());
            _log.OnSwing(attacker, new Serial(reader.ReadUInt32()));
        }
    }
}
