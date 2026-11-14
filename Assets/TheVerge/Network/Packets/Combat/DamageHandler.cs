using TheVerge.Core.Combat;
using TheVerge.Core.Primitives;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Combat
{
    /// <summary>
    /// 0x0B Damage: моб, ushort урон. Сервер: Projects/Server/Network/Packets/OutgoingDamagePackets.cs, SendDamage (клиент
    /// 5.0.0a+). Шлётся только при настройке visibleDamage (по умолчанию — с AOS; в UOR выключено).
    /// </summary>
    public sealed class DamageHandler
    {
        public const byte PacketId = 0x0B;

        private readonly CombatLog _log;

        public DamageHandler(CombatLog log)
        {
            _log = log;
        }

        public void Handle(ref PacketReader reader)
        {
            var mobile = new Serial(reader.ReadUInt32());
            _log.OnDamage(mobile, reader.ReadUInt16());
        }
    }
}
