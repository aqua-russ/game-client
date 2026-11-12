using NUnit.Framework;
using TheVerge.Core.Session;
using TheVerge.Network.Framing;
using TheVerge.Network.Session;
using TheVerge.Network.Transport;

namespace TheVerge.Tests.Network.Session
{
    /// <summary>
    /// Сессия сообщает ядру о соединении (PLAN Стадия 1, пп. 5, 11): подключение — сразу, по порядку с пакетами (на него
    /// клиент шлёт seed, чтобы не молчать); конец соединения — с причиной глазами ядра, без типов транспорта.
    /// </summary>
    public sealed class NetworkSessionObserverTests
    {
        [Test]
        public void Connected_ObserverToldOnce()
        {
            var connection = new FakeConnection();
            var observer = new RecordingObserver();
            var session = NewSession(connection, observer);
            connection.Push(InboundEvent.Connected());

            session.ProcessNext();

            Assert.That(observer.Events, Is.EqualTo(new[] { "connected" }));
        }

        [TestCase(ConnectionCloseReason.Cancelled, DisconnectReason.Requested)]
        [TestCase(ConnectionCloseReason.ConnectFailed, DisconnectReason.ConnectFailed)]
        [TestCase(ConnectionCloseReason.ConnectTimeout, DisconnectReason.ConnectTimeout)]
        [TestCase(ConnectionCloseReason.ServerClosed, DisconnectReason.ServerClosed)]
        [TestCase(ConnectionCloseReason.SocketError, DisconnectReason.NetworkError)]
        public void Closed_ObserverToldReason(ConnectionCloseReason closed, DisconnectReason expected)
        {
            var connection = new FakeConnection();
            var observer = new RecordingObserver();
            var session = NewSession(connection, observer);
            connection.Push(InboundEvent.Closed(closed, "подробности"));

            session.ProcessNext();

            Assert.That(observer.Events, Is.EqualTo(new[] { expected + ":подробности" }));
        }

        [Test]
        public void BrokenStream_ObserverToldProtocolError()
        {
            var connection = new FakeConnection();
            var observer = new RecordingObserver();
            var session = NewSession(connection, observer);
            var error = new FramingError(FramingErrorKind.UnknownPacket, 0x02, 0, 0, new byte[] { 0x02 });
            connection.Push(InboundEvent.FramingFailed(error));

            session.ProcessNext();

            Assert.That(observer.Events.Count, Is.EqualTo(1));
            Assert.That(observer.Events[0], Does.StartWith(DisconnectReason.ProtocolError + ":").And.Contain("0x02"));
        }

        private static NetworkSession NewSession(FakeConnection connection, RecordingObserver observer)
        {
            var log = new RecordingLog();
            return new NetworkSession(connection, new PacketDispatcher(log), log) { Observer = observer };
        }
    }
}
