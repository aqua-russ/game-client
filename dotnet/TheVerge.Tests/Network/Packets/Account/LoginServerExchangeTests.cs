using System;
using System.Linq;
using NUnit.Framework;
using TheVerge.Core.Session;
using TheVerge.Network.Packets.Account;
using TheVerge.Network.Session;
using TheVerge.Network.Transport;
using TheVerge.Tests.Core.Session;
using TheVerge.Tests.Network.Session;
using TheVerge.Tests.Network.Transport;

namespace TheVerge.Tests.Network.Packets.Account
{
    /// <summary>
    /// Логин-сервер целиком через сетевую часть (PLAN Стадия 1, п. 5): подключение → в том же тике одной отправкой seed
    /// и логин (сервер блокирует подключение, которое молчит 5 с: NetState.Network.cs, DisconnectUnattachedSockets);
    /// отказ 0x82 — OutgoingAccountPackets.cs, SendAccountLoginRejected: номер и байт причины ALRReason.
    /// </summary>
    public sealed class LoginServerExchangeTests
    {
        private const int Seed = 0x01020304;

        [Test]
        public void Connected_SeedAndLoginGoOutInSameTick_OneSend()
        {
            var (connection, _, client) = NewClient();
            connection.Push(InboundEvent.Connected());

            client.Tick(0);

            Assert.That(connection.Sent.Count, Is.EqualTo(1));
            var sent = connection.Sent[0];
            Assert.That(sent.Length, Is.EqualTo(21 + 62));
            Assert.That(sent[0], Is.EqualTo(0xEF));
            Assert.That(sent.Skip(1).Take(4), Is.EqualTo(new byte[] { 0x01, 0x02, 0x03, 0x04 }));
            Assert.That(sent[21], Is.EqualTo(0x80));
            Assert.That(sent.Skip(22).Take(6), Is.EqualTo(new[] { (byte)'p', (byte)'l', (byte)'a', (byte)'y', (byte)'e', (byte)'r' }));
        }

        [Test]
        public void LoginRejected_FlowFailsWithReason()
        {
            var (connection, flow, client) = NewClient();
            connection.Push(InboundEvent.Connected());
            connection.Push(InboundEvent.FromPacket(new byte[] { 0x82, 0x03 }, 2, new CountingArrayPool()));
            connection.Push(InboundEvent.Closed(ConnectionCloseReason.ServerClosed, string.Empty));

            client.Tick(0);

            Assert.That(flow.State, Is.EqualTo(LoginState.Failed));
            Assert.That(flow.Error.Kind, Is.EqualTo(LoginErrorKind.LoginDenied));
            Assert.That((LoginRejectReason)flow.Error.Code, Is.EqualTo(LoginRejectReason.BadPassword));
        }

        private static (FakeConnection, LoginFlow, GameClient) NewClient()
        {
            var connection = new FakeConnection();
            var log = new RecordingLog();
            var dispatcher = new PacketDispatcher(log);
            var session = new NetworkSession(connection, dispatcher, log);
            var flow = new LoginFlow(new AccountSender(session), new RecordingConnector(), Seed);
            dispatcher.Register(LoginRejectedHandler.PacketId, new LoginRejectedHandler(flow).Handle);
            session.Observer = flow;
            flow.Start("127.0.0.1", 2593, new LoginCredentials("player", "secret"));
            return (connection, flow, new GameClient(session, new FakeClock()));
        }
    }
}
