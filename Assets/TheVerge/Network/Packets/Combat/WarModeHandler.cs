using TheVerge.Core.Combat;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Combat
{
    /// <summary>
    /// 0x72 War Mode: bool стойка, дальше 0x00 0x32 0x00. Сервер: Projects/Server/Network/Packets/OutgoingCombatPackets.cs,
    /// SendSetWarMode (при входе и при каждой смене стойки — Mobile.Warmode).
    /// </summary>
    public sealed class WarModeHandler
    {
        public const byte PacketId = 0x72;

        private readonly CombatState _state;

        public WarModeHandler(CombatState state)
        {
            _state = state;
        }

        public void Handle(ref PacketReader reader) => _state.OnWarMode(reader.ReadBoolean());
    }
}
