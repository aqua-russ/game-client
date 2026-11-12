using System;
using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.Session;

namespace TheVerge.Tests.Core.Session
{
    /// <summary>
    /// Логин-сервер (PLAN Стадия 1, п. 5). Сервер ждёт первым seed с версией (IncomingAccountPackets.cs, LoginServerSeed;
    /// seed 0 — отключение), затем логин (AccountLogin); на неверный логин отвечает отказом с причиной ALRReason
    /// (OutgoingAccountPackets.cs) и сам закрывает соединение. Подключение, которое 5 с молчит, сервер блокирует
    /// (NetState.Network.cs, DisconnectUnattachedSockets), поэтому seed и логин уходят сразу после подключения.
    /// </summary>
    public sealed class LoginFlowLoginServerTests
    {
        private const int Seed = 0x0A0B0C0D;

        private RecordingAccountSender _sender = new RecordingAccountSender();
        private RecordingConnector _connector = new RecordingConnector();
        private LoginFlow _flow = null!;
        private List<LoginState> _states = new List<LoginState>();

        [SetUp]
        public void SetUp()
        {
            _sender = new RecordingAccountSender();
            _connector = new RecordingConnector();
            _flow = new LoginFlow(_sender, _connector, Seed);
            _states = new List<LoginState>();
            _flow.StateChanged += _states.Add;
        }

        [Test]
        public void Start_ConnectsToLoginServer_SendsNothingYet()
        {
            _flow.Start("127.0.0.1", 2593, Credentials());

            Assert.That(_connector.Endpoints, Is.EqualTo(new[] { "127.0.0.1:2593" }));
            Assert.That(_sender.Calls, Is.Empty);
            Assert.That(_flow.State, Is.EqualTo(LoginState.ConnectingLogin));
        }

        [Test]
        public void Connected_SendsSeedThenLogin_WithTargetVersion()
        {
            _flow.Start("127.0.0.1", 2593, Credentials());

            _flow.OnConnected();

            Assert.That(_sender.Calls, Is.EqualTo(new[] { "seed", "login" }));
            Assert.That(_sender.Seed, Is.EqualTo(Seed));
            Assert.That(_sender.Version.ToString(), Is.EqualTo("7.0.95.0"));
            Assert.That(_sender.Credentials.Username, Is.EqualTo("player"));
            Assert.That(_sender.Credentials.Password, Is.EqualTo("secret"));
            Assert.That(_flow.State, Is.EqualTo(LoginState.AwaitingShardList));
            Assert.That(_states, Is.EqualTo(new[] { LoginState.ConnectingLogin, LoginState.AwaitingShardList }));
        }

        [Test]
        public void ZeroSeed_Refused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(new Action(() => { _ = new LoginFlow(_sender, _connector, 0); }));
        }

        [Test]
        public void StartTwice_Refused()
        {
            _flow.Start("127.0.0.1", 2593, Credentials());

            Assert.Throws<InvalidOperationException>(new Action(() => _flow.Start("127.0.0.1", 2593, Credentials())));
            Assert.That(_connector.Endpoints.Count, Is.EqualTo(1));
        }

        [Test]
        public void LoginRejected_BadPassword_FailsWithServerCode()
        {
            StartAndConnect();

            _flow.OnLoginRejected(LoginRejectReason.BadPassword);

            Assert.That(_flow.State, Is.EqualTo(LoginState.Failed));
            Assert.That(_flow.Error.Kind, Is.EqualTo(LoginErrorKind.LoginDenied));
            Assert.That(_flow.Error.Code, Is.EqualTo(3));
        }

        [Test]
        public void LoginRejected_ThenServerCloses_ErrorStaysLoginDenied()
        {
            StartAndConnect();
            _flow.OnLoginRejected(LoginRejectReason.BadCommunication);

            _flow.OnDisconnected(DisconnectReason.ServerClosed, string.Empty);

            Assert.That(_flow.Error.Kind, Is.EqualTo(LoginErrorKind.LoginDenied));
            Assert.That(_flow.Error.Code, Is.EqualTo(255));
            Assert.That(_states, Is.EqualTo(new[] { LoginState.ConnectingLogin, LoginState.AwaitingShardList, LoginState.Failed }));
        }

        [TestCase(DisconnectReason.ConnectFailed, LoginErrorKind.ConnectFailed)]
        [TestCase(DisconnectReason.ConnectTimeout, LoginErrorKind.ConnectFailed)]
        public void ConnectFails_ConnectFailed(DisconnectReason reason, LoginErrorKind expected)
        {
            _flow.Start("127.0.0.1", 2593, Credentials());

            _flow.OnDisconnected(reason, "нет ответа");

            Assert.That(_flow.State, Is.EqualTo(LoginState.Failed));
            Assert.That(_flow.Error.Kind, Is.EqualTo(expected));
            Assert.That(_flow.Error.Detail, Is.EqualTo("нет ответа"));
        }

        [TestCase(DisconnectReason.ServerClosed, LoginErrorKind.Disconnected)]
        [TestCase(DisconnectReason.NetworkError, LoginErrorKind.Disconnected)]
        [TestCase(DisconnectReason.ProtocolError, LoginErrorKind.ProtocolError)]
        public void ConnectionLostWhileWaiting_TypedError(DisconnectReason reason, LoginErrorKind expected)
        {
            StartAndConnect();

            _flow.OnDisconnected(reason, string.Empty);

            Assert.That(_flow.State, Is.EqualTo(LoginState.Failed));
            Assert.That(_flow.Error.Kind, Is.EqualTo(expected));
            Assert.That(_flow.Error.Code, Is.EqualTo((int)reason));
        }

        [Test]
        public void ClientClosesItself_ClosedWithoutError()
        {
            StartAndConnect();

            _flow.OnDisconnected(DisconnectReason.Requested, string.Empty);

            Assert.That(_flow.State, Is.EqualTo(LoginState.Closed));
            Assert.That(_flow.Error.Kind, Is.EqualTo(LoginErrorKind.None));
        }

        [Test]
        public void Credentials_LongerThanServerField_Refused()
        {
            Assert.Throws<ArgumentException>(new Action(() => { _ = new LoginCredentials(new string('a', 31), "secret"); }));
            Assert.Throws<ArgumentException>(new Action(() => { _ = new LoginCredentials("player", new string('b', 31)); }));
            Assert.DoesNotThrow(new Action(() => { _ = new LoginCredentials(new string('a', 30), new string('b', 30)); }));
        }

        [Test]
        public void Credentials_EmptyOrNotLatin1_Refused()
        {
            Assert.Throws<ArgumentException>(new Action(() => { _ = new LoginCredentials(string.Empty, "secret"); }));
            Assert.Throws<ArgumentException>(new Action(() => { _ = new LoginCredentials("player", string.Empty); }));
            Assert.Throws<ArgumentException>(new Action(() => { _ = new LoginCredentials("игрок", "secret"); }));
            Assert.DoesNotThrow(new Action(() => { _ = new LoginCredentials("Jörg", "pässwört"); }));
        }

        [Test]
        public void Credentials_ToStringHidesPassword()
        {
            Assert.That(Credentials().ToString(), Does.Contain("player").And.Not.Contain("secret"));
        }

        private void StartAndConnect()
        {
            _flow.Start("127.0.0.1", 2593, Credentials());
            _flow.OnConnected();
        }

        private static LoginCredentials Credentials() => new LoginCredentials("player", "secret");
    }
}
