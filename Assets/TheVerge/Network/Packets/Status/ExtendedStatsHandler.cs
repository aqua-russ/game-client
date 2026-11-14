using TheVerge.Core.Primitives;
using TheVerge.Core.Status;
using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Status
{
    /// <summary>
    /// 0xBF 0x19 Extended Stats: byte команда; 0 — Serial и мёртвый ли привязанный питомец (OutgoingMobilePackets.cs,
    /// SendBondedStatus); 2 — Serial, 0, биты замков «сила &lt;&lt; 4 | ловкость &lt;&lt; 2 | интеллект» (OutgoingPlayerPackets.cs,
    /// SendStatLockInfo). Других команд сервер не шлёт — такие пропускаются.
    /// </summary>
    public sealed class ExtendedStatsHandler
    {
        public const byte PacketId = 0xBF;
        public const ushort SubCommand = 0x19;

        private readonly MobileStatusUpdates _updates;

        public ExtendedStatsHandler(MobileStatusUpdates updates)
        {
            _updates = updates;
        }

        public void Handle(ref PacketReader reader)
        {
            var command = reader.ReadByte();
            if (command == 0)
            {
                var serial = new Serial(reader.ReadUInt32());
                _updates.OnBonded(serial, reader.ReadBoolean());
            }
            else if (command == 2)
            {
                var serial = new Serial(reader.ReadUInt32());
                reader.ReadByte();
                var bits = reader.ReadByte();
                _updates.OnStatLocks(serial, new StatLocks(Lock(bits >> 4), Lock(bits >> 2), Lock(bits)));
            }
        }

        private static StatLock Lock(int bits) => (StatLock)(bits & 3);
    }
}
