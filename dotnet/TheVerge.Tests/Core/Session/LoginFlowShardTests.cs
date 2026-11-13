using System;
using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.Session;

namespace TheVerge.Tests.Core.Session
{
    /// <summary>
    /// Список шардов и редирект (PLAN Стадия 1, п. 6). На верный логин сервер шлёт список шардов
    /// (OutgoingAccountPackets.cs, SendAccountLoginAck), клиент выбирает шард по его номеру (IncomingAccountPackets.cs,
    /// PlayServer: ReadInt16 — номер в списке сервера, неверный — отключение), сервер отвечает адресом игрового сервера с
    /// ключом (SendPlayServerAck) и сам закрывает логин-соединение (NetState.cs, LoginServer_AwaitingServerSelect →
    /// Disconnect).
    /// </summary>
    public sealed class LoginFlowShardTests
    {
        private RecordingAccountSender _sender = new RecordingAccountSender();
        private LoginFlow _flow = null!;
        private List<LoginState> _states = new List<LoginState>();

        [SetUp]
        public void SetUp()
        {
            _sender = new RecordingAccountSender();
            _flow = new LoginFlow(_sender, new RecordingConnector(), 1);
            _states = new List<LoginState>();
            _flow.Start("127.0.0.1", 2593, new LoginCredentials("player", "secret"));
            _flow.OnConnected();
            _flow.StateChanged += _states.Add;
            _sender.Calls.Clear();
        }

        [Test]
        public void ShardList_StoredAndAwaitsChoice()
        {
            _flow.OnShardList(TwoShards());

            Assert.That(_flow.State, Is.EqualTo(LoginState.ShardSelection));
            Assert.That(_flow.Shards.Count, Is.EqualTo(2));
            Assert.That(_flow.Shards[1].Name, Is.EqualTo("Second"));
            Assert.That(_sender.Calls, Is.Empty, "шард выбирает игрок или бот, не сам вход");
        }

        [Test]
        public void SelectShard_SendsServerIndexOfChosenEntry()
        {
            _flow.OnShardList(TwoShards());

            _flow.SelectShard(1);

            Assert.That(_sender.Calls, Is.EqualTo(new[] { "shard" }));
            Assert.That(_sender.ShardIndex, Is.EqualTo(1));
            Assert.That(_flow.State, Is.EqualTo(LoginState.AwaitingRedirect));
        }

        [Test]
        public void SelectShard_BeforeList_RefusedNothingSent()
        {
            Assert.Throws<InvalidOperationException>(new Action(() => _flow.SelectShard(0)));
            Assert.That(_sender.Calls, Is.Empty);
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void SelectShard_OutsideList_RefusedNothingSent(int position)
        {
            _flow.OnShardList(TwoShards());

            Assert.Throws<ArgumentOutOfRangeException>(new Action(() => _flow.SelectShard(position)));
            Assert.That(_sender.Calls, Is.Empty);
            Assert.That(_flow.State, Is.EqualTo(LoginState.ShardSelection));
        }

        [Test]
        public void SelectShard_Twice_SecondRefused()
        {
            // Сервер выбирает шард один раз на соединение: второй выбор — отключение (PlayServer, AuthId != 0).
            _flow.OnShardList(TwoShards());
            _flow.SelectShard(0);

            Assert.Throws<InvalidOperationException>(new Action(() => _flow.SelectShard(0)));
            Assert.That(_sender.Calls, Is.EqualTo(new[] { "shard" }));
        }

        [Test]
        public void EmptyShardList_ProtocolError()
        {
            _flow.OnShardList(Array.Empty<ShardEntry>());

            Assert.That(_flow.State, Is.EqualTo(LoginState.Failed));
            Assert.That(_flow.Error.Kind, Is.EqualTo(LoginErrorKind.ProtocolError));
        }

        [Test]
        public void Redirect_StoredHostPortKey()
        {
            _flow.OnShardList(TwoShards());
            _flow.SelectShard(0);

            _flow.OnRedirect(new ServerRedirect("127.0.0.1", 2593, 0x12345678));

            Assert.That(_flow.Redirect.Host, Is.EqualTo("127.0.0.1"));
            Assert.That(_flow.Redirect.Port, Is.EqualTo(2593));
            Assert.That(_flow.Redirect.AuthKey, Is.EqualTo(0x12345678));
            Assert.That(_states, Does.Contain(LoginState.Redirected));
            Assert.That(_flow.Redirect.ToString(), Does.Not.Contain("12345678").And.Not.Contain(0x12345678.ToString()));
        }

        [Test]
        public void Redirect_FlowLeavesLoginServer_ForGameServer()
        {
            // Закрытие логин-соединения сервером после редиректа — не ошибка: сессия к этому времени уже на игровом
            // соединении (NetworkSession.Attach), см. ShardExchangeTests. Здесь — что вход ушёл с логин-сервера.
            var connector = new RecordingConnector();
            var flow = new LoginFlow(_sender, connector, 1);
            flow.Start("127.0.0.1", 2593, new LoginCredentials("player", "secret"));
            flow.OnConnected();
            flow.OnShardList(TwoShards());
            flow.SelectShard(0);

            flow.OnRedirect(new ServerRedirect("127.0.0.1", 2593, 7));

            Assert.That(connector.Endpoints.Count, Is.EqualTo(2), "второе подключение — к игровому серверу");
            Assert.That(flow.Error.Kind, Is.EqualTo(LoginErrorKind.None));
        }

        [Test]
        public void ServerClosesBeforeRedirect_Disconnected()
        {
            _flow.OnShardList(TwoShards());
            _flow.SelectShard(0);

            _flow.OnDisconnected(DisconnectReason.ServerClosed, string.Empty);

            Assert.That(_flow.Error.Kind, Is.EqualTo(LoginErrorKind.Disconnected));
        }

        [Test]
        public void ShardListOrRedirect_OutOfOrder_Ignored()
        {
            _flow.OnRedirect(new ServerRedirect("10.0.0.1", 1, 1));

            Assert.That(_flow.State, Is.EqualTo(LoginState.AwaitingShardList));
            Assert.That(_flow.Redirect.Host, Is.Null);
        }

        [Test]
        public void MalformedPacket_ProtocolErrorWithDetail()
        {
            _flow.OnMalformedPacket("0xA8: число шардов не совпадает с длиной");

            Assert.That(_flow.State, Is.EqualTo(LoginState.Failed));
            Assert.That(_flow.Error.Kind, Is.EqualTo(LoginErrorKind.ProtocolError));
            Assert.That(_flow.Error.Detail, Does.Contain("0xA8"));
        }

        private static IReadOnlyList<ShardEntry> TwoShards() => new[]
        {
            new ShardEntry(0, "First", 0, 3, "127.0.0.1"),
            new ShardEntry(1, "Second", 10, 3, "127.0.0.1"),
        };
    }
}
