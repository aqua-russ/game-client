using System.Linq;
using NUnit.Framework;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Core.World;
using TheVerge.Network.Framing;
using TheVerge.Network.IO;
using TheVerge.Network.Packets.Session;
using TheVerge.Network.Session;
using TheVerge.Network.Transport;
using TheVerge.Tests.Core.Session;
using TheVerge.Tests.Network.Session;
using TheVerge.Tests.Network.Transport;

namespace TheVerge.Tests.Network.Packets.Session
{
    /// <summary>
    /// Ping 0x73 и выход 0xD1 по байтам сервера: PingReq читает номер и возвращает его (SendPingAck: 0x73, номер),
    /// LogoutReq отвечает 0xD1 0x01 (OutgoingPlayerPackets.cs); длины 2 (IncomingPlayerPackets.cs, Register).
    /// </summary>
    public sealed class KeepAliveLogoutPacketTests
    {
        [Test]
        public void Ping_2Bytes_WithSequence()
        {
            var bytes = PacketBytes.Write((ref PacketWriter w) => PingPacket.Write(ref w, 0x2A));

            Assert.That(bytes, Is.EqualTo(new byte[] { 0x73, 0x2A }));
            Assert.That(bytes.Length, Is.EqualTo(PacketTable.Get(PacketDirection.ClientToServer, 0x73).FixedLength));
        }

        [Test]
        public void Logout_2Bytes()
        {
            var bytes = PacketBytes.Write((ref PacketWriter w) => LogoutPacket.Write(ref w));

            Assert.That(bytes[0], Is.EqualTo(0xD1));
            Assert.That(bytes.Length, Is.EqualTo(PacketTable.Get(PacketDirection.ClientToServer, 0xD1).FixedLength));
        }

        [Test]
        public void InWorld_PingGoesOut_EchoGivesRtt_ThenLogoutAckClosesConnection()
        {
            var clock = new FakeClock { NowMs = 0 };
            var connection = new FakeConnection();
            var log = new RecordingLog();
            var dispatcher = new PacketDispatcher(log);
            var session = new NetworkSession(dispatcher, log);
            var connector = new ServerConnector(session, () => connection, System.Threading.CancellationToken.None);
            var flow = new LoginFlow(new RecordingAccountSender(), connector, 1);
            var sender = new SessionSender(session);
            var world = new WorldEntry(flow.Progress, sender);
            var game = new GameSession(flow.Progress, sender, connector, clock);
            SessionHandlers.Register(dispatcher, world, new ServerRequests(sender), game);
            session.Observer = flow;
            var client = new GameClient(session, clock, game);
            EnterWorld(flow, world, connection, client);

            clock.NowMs = ClientLimits.PingIntervalMs;
            client.Tick(clock.NowMs);
            var ping = connection.Sent.Last();
            Assert.That(ping[0], Is.EqualTo(0x73));
            clock.NowMs += 12;
            connection.Push(Packet(new byte[] { 0x73, ping[1] }));
            client.Tick(clock.NowMs);
            Assert.That(game.LastRttMs, Is.EqualTo(12));

            game.Logout();
            client.Tick(clock.NowMs);
            Assert.That(connection.Sent.Last()[0], Is.EqualTo(0xD1));
            connection.Push(Packet(new byte[] { 0xD1, 0x01 }));
            client.Tick(clock.NowMs);
            client.Tick(clock.NowMs);

            Assert.That(flow.State, Is.EqualTo(LoginState.Closed));
            Assert.That(flow.Error.Kind, Is.EqualTo(LoginErrorKind.None));
            Assert.That(session.IsClosed, Is.True);
        }

        private static void EnterWorld(LoginFlow flow, WorldEntry world, FakeConnection connection, GameClient client)
        {
            flow.Start("127.0.0.1", 2593, new LoginCredentials("player", "secret"));
            connection.Push(InboundEvent.Connected());
            client.Tick(0);
            flow.OnShardList(new[] { new ShardEntry(0, "Shard", 0, 0, "127.0.0.1") });
            flow.SelectShard(0);
            flow.Progress.MoveTo(LoginState.AwaitingCharacterList); // редирект на то же соединение здесь не нужен
            flow.Characters.OnCharacterList(new[] { new CharacterSlot(0, "AQuaruss") }, new StartingCity[0], 0);
            flow.Characters.Select(0);
            world.OnLoginConfirm(new PlayerState(new Serial(1), 0x190, Point3D.Zero, Direction.North, new MapSize(7168, 4096)));
            world.OnLoginComplete();
            client.Tick(0);
        }

        private static InboundEvent Packet(byte[] bytes) => InboundEvent.FromPacket(bytes, bytes.Length, new CountingArrayPool());
    }
}
