using System.Linq;
using NUnit.Framework;
using TheVerge.Core.Primitives;
using TheVerge.Core.Status;
using TheVerge.Network.Packets.Status;
using TheVerge.Network.Session;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Network.Packets.Status
{
    /// <summary>
    /// Запрос статуса моба 0x34 — как читает сервер (IncomingPlayerPackets.cs, MobileQuery, 10 байт): int 0xEDEDEDED,
    /// byte тип (4 — статус), uint серийник. Ответ — краткий или полный 0x11 (Mobile.OnStatsQuery).
    /// </summary>
    public sealed class StatusQueryPacketTests
    {
        [Test]
        public void RequestStatus_TenBytesAsServerReads()
        {
            var session = new NetworkSession(new PacketDispatcher(new RecordingLog()), new RecordingLog());
            var commands = new StatusCommands(new StatusSender(session));

            commands.RequestStatus(new Serial(0x0102));

            Assert.That(session.Outgoing.Pending.ToArray(),
                Is.EqualTo(new byte[] { 0x34, 0xED, 0xED, 0xED, 0xED, 0x04, 0x00, 0x00, 0x01, 0x02 }));
        }

        [Test]
        public void RequestStatus_ZeroSerial_NothingSent()
        {
            var session = new NetworkSession(new PacketDispatcher(new RecordingLog()), new RecordingLog());

            new StatusCommands(new StatusSender(session)).RequestStatus(Serial.Zero);

            Assert.That(session.Outgoing.Pending.ToArray().Any(), Is.False);
        }
    }
}
