using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.Session;

namespace TheVerge.Tests.Core.Session
{
    /// <summary>
    /// Игровой сокет (PLAN Стадия 1, п. 7). По адресу из редиректа — новое подключение; сразу после него ключ и вход на
    /// игровой сервер (IncomingAccountPackets.cs, GameLogin: ключ должен совпасть с seed соединения и с выданным при
    /// выборе шарда). На неверный вход сервер просто закрывает соединение (GameLogin → Disconnect).
    /// </summary>
    public sealed class LoginFlowGameServerTests
    {
        private const int AuthKey = 0x11223344;

        private RecordingAccountSender _sender = new RecordingAccountSender();
        private RecordingConnector _connector = new RecordingConnector();
        private LoginFlow _flow = null!;
        private List<LoginState> _states = new List<LoginState>();

        [SetUp]
        public void SetUp()
        {
            _sender = new RecordingAccountSender();
            _connector = new RecordingConnector();
            _flow = new LoginFlow(_sender, _connector, 1);
            _flow.Start("10.0.0.1", 2593, new LoginCredentials("player", "secret"));
            _flow.OnConnected();
            _flow.OnShardList(new[] { new ShardEntry(0, "Shard", 0, 0, "127.0.0.1") });
            _flow.SelectShard(0);
            _states = new List<LoginState>();
            _flow.StateChanged += _states.Add;
            _sender.Calls.Clear();
        }

        [Test]
        public void Redirect_ConnectsToGameServerFromRedirect()
        {
            _flow.OnRedirect(new ServerRedirect("127.0.0.2", 5000, AuthKey));

            Assert.That(_connector.Endpoints, Is.EqualTo(new[] { "10.0.0.1:2593", "127.0.0.2:5000" }));
            Assert.That(_flow.State, Is.EqualTo(LoginState.ConnectingGame));
            Assert.That(_states, Is.EqualTo(new[] { LoginState.Redirected, LoginState.ConnectingGame }));
            Assert.That(_sender.Calls, Is.Empty, "игровой сервер ещё не подключён");
        }

        [Test]
        public void GameConnected_SendsGameLoginWithKeyAndCredentials_Once()
        {
            _flow.OnRedirect(new ServerRedirect("127.0.0.1", 2593, AuthKey));

            _flow.OnConnected();
            _flow.OnConnected();

            Assert.That(_sender.Calls, Is.EqualTo(new[] { "game" }));
            Assert.That(_sender.AuthKey, Is.EqualTo(AuthKey));
            Assert.That(_sender.Credentials.Username, Is.EqualTo("player"));
            Assert.That(_sender.Credentials.Password, Is.EqualTo("secret"));
            Assert.That(_flow.State, Is.EqualTo(LoginState.AwaitingCharacterList));
        }

        [TestCase(DisconnectReason.ConnectFailed, LoginErrorKind.ConnectFailed)]
        [TestCase(DisconnectReason.ConnectTimeout, LoginErrorKind.ConnectFailed)]
        public void GameServerUnreachable_ConnectFailed(DisconnectReason reason, LoginErrorKind expected)
        {
            _flow.OnRedirect(new ServerRedirect("127.0.0.1", 2593, AuthKey));

            _flow.OnDisconnected(reason, "нет ответа");

            Assert.That(_flow.State, Is.EqualTo(LoginState.Failed));
            Assert.That(_flow.Error.Kind, Is.EqualTo(expected));
        }

        [Test]
        public void GameServerClosesAfterGameLogin_Disconnected()
        {
            _flow.OnRedirect(new ServerRedirect("127.0.0.1", 2593, AuthKey));
            _flow.OnConnected();

            _flow.OnDisconnected(DisconnectReason.ServerClosed, string.Empty);

            Assert.That(_flow.State, Is.EqualTo(LoginState.Failed));
            Assert.That(_flow.Error.Kind, Is.EqualTo(LoginErrorKind.Disconnected));
        }
    }
}
