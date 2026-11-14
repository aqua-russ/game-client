using TheVerge.Core.Combat;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Combat
{
    /// <summary>
    /// 0x2C Death Status: byte 2 — свой персонаж умер. Сервер: Projects/Server/Network/Packets/OutgoingPlayerPackets.cs,
    /// SendDeathStatus (Mobile.OnDeath; дальше — тело призрака, саван, здоровье 0 обычными пакетами). Ответ клиента
    /// (0x2C) сервер игнорирует — не шлём.
    /// </summary>
    public sealed class DeathStatusHandler
    {
        public const byte PacketId = 0x2C;

        private readonly CombatState _state;

        public DeathStatusHandler(CombatState state)
        {
            _state = state;
        }

        public void Handle(ref PacketReader reader)
        {
            reader.Skip(1);
            _state.OnDeathStatus();
        }
    }
}
