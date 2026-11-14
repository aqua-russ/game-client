using TheVerge.Core.Primitives;
using TheVerge.Core.Status;
using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Status
{
    /// <summary>
    /// 0xA1 Mobile Hits, 0xA2 Mobile Mana, 0xA3 Mobile Stamina — одно значение: Serial, максимум, текущее (short).
    /// Сервер: Projects/Server/Network/Packets/OutgoingMobilePackets.cs, CreateMobileHits/Mana/Stam (WriteAttribute без
    /// reverse); чужим — долей от 100.
    /// </summary>
    public sealed class MobileVitalHandler
    {
        public const byte HitsPacketId = 0xA1;
        public const byte ManaPacketId = 0xA2;
        public const byte StamPacketId = 0xA3;

        private readonly MobileStatusUpdates _updates;
        private readonly VitalKind _kind;

        public MobileVitalHandler(MobileStatusUpdates updates, VitalKind kind)
        {
            _updates = updates;
            _kind = kind;
        }

        public void Handle(ref PacketReader reader)
        {
            var serial = new Serial(reader.ReadUInt32());
            var maximum = reader.ReadInt16();
            _updates.OnVital(serial, _kind, new Vital(reader.ReadInt16(), maximum));
        }
    }
}
