using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    /// Шард, редирект и игровой сокет через сетевую часть (PLAN Стадия 1, пп. 6–7): список 0xA8 → выбор 0xA0 уходит в конце
    /// тика → 0x8C, после которого сервер закрывает логин-соединение (NetState.cs, LoginServer_AwaitingServerSelect →
    /// Disconnect) — это не ошибка: клиент уже перешёл на новое соединение с игровым сервером и шлёт туда ключ и 0x91.
    /// </summary>
    public sealed class ShardExchangeTests
    {
        private static readonly byte[] Localhost = { 127, 0, 0, 1 };

        [Test]
        public void Redirect_LoginSocketClosedByServer_NoError_GameLoginOnNewConnection()
        {
            var login = new FakeConnection();
            var game = new FakeConnection();
            var connections = new Queue<FakeConnection>(new[] { login, game });
            var log = new RecordingLog();
            var dispatcher = new PacketDispatcher(log);
            var session = new NetworkSession(dispatcher, log);
            var flow = new LoginFlow(new AccountSender(session), new ServerConnector(session, () => connections.Dequeue(), CancellationToken.None), 1);
            AccountHandlers.Register(dispatcher, flow);
            session.Observer = flow;
            var client = new GameClient(session, new FakeClock());
            flow.Start("127.0.0.1", 2593, new LoginCredentials("player", "secret"));
            login.Push(InboundEvent.Connected());
            login.Push(Packet(AccountServerSamples.ServerList(("Remaster UO", 0, 3, Localhost))));
            client.Tick(0);

            flow.SelectShard(0);
            client.Tick(0);
            login.Push(Packet(AccountServerSamples.PlayServerAck(Localhost, 2593, 77)));
            login.Push(InboundEvent.Closed(ConnectionCloseReason.ServerClosed, string.Empty));
            client.Tick(0);
            game.Push(InboundEvent.Connected());
            client.Tick(0);

            Assert.That(login.Sent.Last(), Is.EqualTo(new byte[] { 0xA0, 0x00, 0x00 }));
            Assert.That(login.Disposed, Is.True, "логин-соединение закрыто");
            Assert.That(game.ConnectedTo, Is.EqualTo("127.0.0.1:2593"));
            Assert.That(game.Sent.Single().Take(5), Is.EqualTo(new byte[] { 0x00, 0x00, 0x00, 77, 0x91 }));
            Assert.That(flow.State, Is.EqualTo(LoginState.AwaitingCharacterList));
            Assert.That(flow.Error.Kind, Is.EqualTo(LoginErrorKind.None));
        }

        private static InboundEvent Packet(byte[] bytes) => InboundEvent.FromPacket(bytes, bytes.Length, new CountingArrayPool());
    }
}
