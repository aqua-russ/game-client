using TheVerge.Core.Primitives;
using TheVerge.Core.Status;
using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Status
{
    /// <summary>
    /// 0x2D Mobile Attributes: Serial, затем здоровье, мана, выносливость — каждое «максимум, текущее» (short).
    /// Сервер: Projects/Server/Network/Packets/OutgoingMobilePackets.cs, CreateMobileAttributes.
    /// </summary>
    public sealed class MobileAttributesHandler
    {
        public const byte PacketId = 0x2D;

        private readonly MobileStatusUpdates _updates;

        public MobileAttributesHandler(MobileStatusUpdates updates)
        {
            _updates = updates;
        }

        public void Handle(ref PacketReader reader)
        {
            var serial = new Serial(reader.ReadUInt32());
            var hits = ReadVital(ref reader);
            var mana = ReadVital(ref reader);
            _updates.OnAttributes(serial, hits, mana, ReadVital(ref reader));
        }

        private static Vital ReadVital(ref PacketReader reader)
        {
            var maximum = reader.ReadInt16();
            return new Vital(reader.ReadInt16(), maximum);
        }
    }
}
