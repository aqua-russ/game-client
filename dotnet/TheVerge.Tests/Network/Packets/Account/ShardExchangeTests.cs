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
    /// Шард и редирект через сетевую часть (PLAN Стадия 1, п. 6): список 0xA8 → выбор 0xA0 уходит в конце тика → 0x8C и
    /// закрытие логин-соединения сервером (NetState.cs, LoginServer_AwaitingServerSelect → Disconnect) — без ошибки.
    /// </summary>
    public sealed class ShardExchangeTests
    {
        private static readonly byte[] Localhost = { 127, 0, 0, 1 };

        [Test]
        public void ShardListSelectRedirect_ThenServerCloses_NoError()
        {
            var connection = new FakeConnection();
            var log = new RecordingLog();
            var dispatcher = new PacketDispatcher(log);
            var session = new NetworkSession(connection, dispatcher, log);
            var flow = new LoginFlow(new AccountSender(session), new RecordingConnector(), 1);
            AccountHandlers.Register(dispatcher, flow);
            session.Observer = flow;
            var client = new GameClient(session, new FakeClock());
            flow.Start("127.0.0.1", 2593, new LoginCredentials("player", "secret"));
            connection.Push(InboundEvent.Connected());
            connection.Push(Packet(AccountServerSamples.ServerList(("Remaster UO", 0, 3, Localhost))));
            client.Tick(0);

            flow.SelectShard(0);
            client.Tick(0);
            connection.Push(Packet(AccountServerSamples.PlayServerAck(Localhost, 2593, 77)));
            connection.Push(InboundEvent.Closed(ConnectionCloseReason.ServerClosed, string.Empty));
            client.Tick(0);

            Assert.That(connection.Sent.Last(), Is.EqualTo(new byte[] { 0xA0, 0x00, 0x00 }));
            Assert.That(flow.Redirect.ToString(), Is.EqualTo("127.0.0.1:2593"));
            Assert.That(flow.Redirect.AuthKey, Is.EqualTo(77));
            Assert.That(flow.Error.Kind, Is.EqualTo(LoginErrorKind.None));
        }

        private static InboundEvent Packet(byte[] bytes) => InboundEvent.FromPacket(bytes, bytes.Length, new CountingArrayPool());
    }
}
