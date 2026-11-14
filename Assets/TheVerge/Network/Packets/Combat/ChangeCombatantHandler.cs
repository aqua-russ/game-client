using TheVerge.Core.Combat;
using TheVerge.Core.Primitives;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Combat
{
    /// <summary>
    /// 0xAA Change Combatant: Serial противника, 0 — противника нет. Сервер: Projects/Server/Network/Packets/
    /// OutgoingCombatPackets.cs, SendChangeCombatant (Mobile.Combatant).
    /// </summary>
    public sealed class ChangeCombatantHandler
    {
        public const byte PacketId = 0xAA;

        private readonly CombatState _state;

        public ChangeCombatantHandler(CombatState state)
        {
            _state = state;
        }

        public void Handle(ref PacketReader reader) => _state.OnCombatant(new Serial(reader.ReadUInt32()));
    }
}
