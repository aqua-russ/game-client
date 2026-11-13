using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using TheVerge.Network.Session;
using TheVerge.Network.Transport;

namespace TheVerge.Tests.Network.Session
{
    /// <summary>
    /// Подключение для входа (PLAN Стадия 1, пп. 5, 7): одно соединение — одно подключение (IConnection), поэтому каждое
    /// подключение — новое соединение; сессия переходит на него, прежнее закрывается, и сессия снова не «закрыта».
    /// </summary>
    public sealed class ServerConnectorTests
    {
        private readonly List<FakeConnection> _made = new List<FakeConnection>();
        private RecordingLog _log = new RecordingLog();
        private RecordingObserver _observer = new RecordingObserver();

        [SetUp]
        public void SetUp()
        {
            _made.Clear();
            _log = new RecordingLog();
            _observer = new RecordingObserver();
        }

        [Test]
        public void Disconnect_ClosesCurrentConnection_AsRequestedByClient()
        {
            // Выход из игры (PLAN Стадия 1, п. 10): после ответа сервера на 0xD1 соединение закрывает клиент.
            var session = NewSession();
            var connector = new ServerConnector(session, NewConnection, CancellationToken.None);
            connector.Connect("127.0.0.1", 2593);
            _made[0].Push(InboundEvent.Connected());
            session.ProcessNext();

            connector.Disconnect();
            while (session.ProcessNext())
            {
            }

            Assert.That(_observer.Events, Is.EqualTo(new[] { "connected", "Requested:" }));
            Assert.That(_made[0].Send(new byte[] { 1 }), Is.False, "после закрытия отправка не проходит");
        }

        [Test]
        public void Disconnect_WithoutConnection_Nothing()
        {
            var connector = new ServerConnector(NewSession(), NewConnection, CancellationToken.None);

            Assert.DoesNotThrow(new System.Action(() => connector.Disconnect()));
        }

        [Test]
        public void Connect_NewConnectionConnectedAndUsedBySession()
        {
            var session = NewSession();
            var connector = new ServerConnector(session, NewConnection, CancellationToken.None);

            connector.Connect("127.0.0.1", 2593);
            _made[0].Push(InboundEvent.Connected());
            session.ProcessNext();

            Assert.That(_made.Count, Is.EqualTo(1));
            Assert.That(_made[0].ConnectedTo, Is.EqualTo("127.0.0.1:2593"));
            Assert.That(_observer.Events, Is.EqualTo(new[] { "connected" }));
        }

        [Test]
        public void ConnectAgain_PreviousClosed_SessionNotClosedAnymore()
        {
            var session = NewSession();
            var connector = new ServerConnector(session, NewConnection, CancellationToken.None);
            connector.Connect("127.0.0.1", 2593);
            _made[0].Push(InboundEvent.Connected());
            _made[0].Push(InboundEvent.FromPacket(new byte[] { 0x55 }, 1, new Transport.CountingArrayPool()));
            session.ProcessNext();

            connector.Connect("10.0.0.2", 2594);

            Assert.That(_made[0].Disposed, Is.True);
            Assert.That(_made[1].ConnectedTo, Is.EqualTo("10.0.0.2:2594"));
            Assert.That(session.IsClosed, Is.False);
            Assert.That(session.ProcessNext(), Is.False, "события прежнего соединения не обрабатываются");
        }

        [Test]
        public void SessionWithoutConnection_NothingToProcess_NotConnected()
        {
            var session = NewSession();

            Assert.That(session.ProcessNext(), Is.False);
            Assert.That(session.IsConnected, Is.False);
            Assert.DoesNotThrow(new System.Action(session.Flush));
            Assert.DoesNotThrow(new System.Action(session.Dispose));
        }

        private NetworkSession NewSession() => new NetworkSession(new PacketDispatcher(_log), _log) { Observer = _observer };

        private IConnection NewConnection()
        {
            var connection = new FakeConnection { State = ConnectionState.Idle };
            _made.Add(connection);
            return connection;
        }
    }
}
