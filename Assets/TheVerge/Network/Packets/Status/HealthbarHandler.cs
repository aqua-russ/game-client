using TheVerge.Core.Primitives;
using TheVerge.Core.Status;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Status
{
    /// <summary>
    /// 0x17 Healthbar: Serial, short «показать» (всегда 1), short вид (1 — яд, 2 — жёлтая), byte уровень (0 — выключено).
    /// Сервер: Projects/Server/Network/Packets/OutgoingMobilePackets.cs, CreateMobileHealthbar; клиентам 7.0+ (StygianAbyss)
    /// яд и неуязвимость приходят только так, а не флагом моба (Mobile.ProcessDelta).
    /// </summary>
    public sealed class HealthbarHandler
    {
        public const byte PacketId = 0x17;

        private readonly MobileStatusUpdates _updates;

        public HealthbarHandler(MobileStatusUpdates updates)
        {
            _updates = updates;
        }

        public void Handle(ref PacketReader reader)
        {
            var serial = new Serial(reader.ReadUInt32());
            reader.ReadInt16();
            var kind = reader.ReadInt16();
            _updates.OnHealthbar(serial, kind, reader.ReadByte());
        }
    }
}
