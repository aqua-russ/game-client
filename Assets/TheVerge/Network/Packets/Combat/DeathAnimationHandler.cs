using TheVerge.Core.Combat;
using TheVerge.Core.Primitives;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Combat
{
    /// <summary>
    /// 0xAF Death Animation: убитый, его труп (0 — трупа нет), int 0. Сервер: Projects/Server/Network/Packets/
    /// OutgoingMobilePackets.cs, CreateDeathAnimation (Mobile.Kill — всем рядом, кроме самого убитого). Труп к этому
    /// времени уже в мире: сервер кладёт его до анимации. Моба не удаляет: NPC сервер удалит сам (0x1D), игрок станет призраком.
    /// </summary>
    public sealed class DeathAnimationHandler
    {
        public const byte PacketId = 0xAF;

        private readonly CombatLog _log;

        public DeathAnimationHandler(CombatLog log)
        {
            _log = log;
        }

        public void Handle(ref PacketReader reader)
        {
            var killed = new Serial(reader.ReadUInt32());
            var corpse = new Serial(reader.ReadUInt32());
            reader.Skip(4);
            _log.OnDeath(killed, corpse);
        }
    }
}
