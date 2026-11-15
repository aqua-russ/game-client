using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Network.Framing;
using TheVerge.Network.IO;
using TheVerge.Network.Session;

namespace TheVerge.Network.Packets.Movement
{
    /// <summary>Порт <see cref="IMovementSender"/> (ADR 0008 п. 1): шаги и ресинхронизация в исходящие тика.</summary>
    public sealed class MovementSender : IMovementSender
    {
        private readonly NetworkSession _session;

        public MovementSender(NetworkSession session)
        {
            _session = session;
        }

        public void SendStep(Direction direction, bool run, byte sequence)
        {
            var writer = Begin(MoveRequestPacket.PacketId);
            MoveRequestPacket.Write(ref writer, direction, run, sequence);
            _session.Outgoing.Commit(writer.Position);
        }

        public void SendResync()
        {
            var writer = Begin(ResyncRequestPacket.PacketId);
            ResyncRequestPacket.Write(ref writer);
            _session.Outgoing.Commit(writer.Position);
        }

        private PacketWriter Begin(byte fixedLengthId) =>
            new PacketWriter(_session.Outgoing.Reserve(PacketTable.Get(PacketDirection.ClientToServer, fixedLengthId).FixedLength));
    }
}
