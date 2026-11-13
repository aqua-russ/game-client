using System;
using System.Buffers.Binary;
using System.Linq;
using System.Text;
using NUnit.Framework;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
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
    /// Вход в мир по байтам сервера: 0x1B — OutgoingAccountPackets.cs, SendLoginConfirmation; 0x55 — SendLoginComplete;
    /// запрос версии 0xBD — SendClientVersionRequest; ответ 0xBD — как читает IncomingAccountPackets.cs, ClientVersion;
    /// язык 0xBF 0x0B — IncomingExtendedCommandPackets.cs, Language (ReadAscii(4)); 0xC8 — IncomingPlayerPackets.cs.
    /// </summary>
    public sealed class WorldEntryPacketTests
    {
        [Test]
        public void LoginConfirm_FieldsAsServerWrote()
        {
            var (flow, world, _, dispatcher, _) = EnteringWorld();

            var packet = WorldSamples.LoginConfirm(0x0000003D, 633, 858, -5);
            packet[17] = 0x84; // направление: юг и бег (SendLoginConfirmation пишет (byte)m.Direction как есть)
            BinaryPrimitives.WriteInt16BigEndian(packet.AsSpan(27), 6144); // ширина карты — не только Фелукка

            dispatcher.Dispatch(packet);

            Assert.That(flow.State, Is.EqualTo(LoginState.WorldLoading));
            var p = world.Player;
            Assert.That(p.Serial.Value, Is.EqualTo(0x3Du));
            Assert.That(p.Body, Is.EqualTo(0x190));
            Assert.That(p.Position, Is.EqualTo(new Point3D(633, 858, -5)));
            Assert.That(p.Direction, Is.EqualTo(Direction.South | Direction.Running));
            Assert.That(p.Map.Width, Is.EqualTo(6144));
            Assert.That(p.Map.Height, Is.EqualTo(4096));
        }

        [Test]
        public void LoginComplete_LanguageAndViewRangeInSameTick()
        {
            var (flow, _, connection, _, client) = EnteringWorld();
            connection.Push(Packet(WorldSamples.LoginConfirm(0x3D, 1, 2, 3)));
            connection.Push(Packet(new byte[] { 0x55 }));

            client.Tick(0);

            Assert.That(flow.State, Is.EqualTo(LoginState.InWorld));
            var sent = connection.Sent.Last();
            Assert.That(sent, Is.EqualTo(new byte[] { 0xBF, 0x00, 0x09, 0x00, 0x0B, (byte)'E', (byte)'N', (byte)'U', 0x00, 0xC8, 18 }));
        }

        [Test]
        public void VersionRequest_AnsweredSameTick_AsciiWithZero()
        {
            var (_, _, connection, _, client) = EnteringWorld();
            connection.Push(Packet(new byte[] { 0xBD, 0x00, 0x03 }));

            client.Tick(0);

            var expected = new byte[] { 0xBD, 0x00, 0x0C }.Concat(Encoding.ASCII.GetBytes("7.0.95.0")).Concat(new byte[] { 0 }).ToArray();
            Assert.That(connection.Sent.Last(), Is.EqualTo(expected));
            var reader = new PacketReader(connection.Sent.Last().AsSpan(3));
            Assert.That(reader.ReadAsciiNullTerminated(), Is.EqualTo("7.0.95.0"), "сервер читает строку до нуля");
        }

        [Test]
        public void VersionRequest_Repeated_NoGarbage()
        {
            // Сервер спрашивает версию и во время ходьбы (замер RTT): ответ — горячий путь без мусора (AGENTS.md §3.1).
            var session = new NetworkSession(new FakeConnection { KeepSent = false }, new PacketDispatcher(new RecordingLog()), new RecordingLog());
            var requests = new ServerRequests(new SessionSender(session));
            requests.OnClientVersionRequest();
            session.Flush();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100; i++)
            {
                requests.OnClientVersionRequest();
                session.Flush();
            }

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        [Test]
        public void OutgoingPackets_MatchMatrix()
        {
            Assert.That(PacketTable.Get(PacketDirection.ClientToServer, 0xC8).FixedLength, Is.EqualTo(2));
            Assert.That(PacketTable.Get(PacketDirection.ClientToServer, 0xBF, 0x0B).IsKnown, Is.True);
            Assert.That(PacketTable.Get(PacketDirection.ClientToServer, 0xBD).IsVariable, Is.True);
        }

        private static (LoginFlow, WorldEntry, FakeConnection, PacketDispatcher, GameClient) EnteringWorld()
        {
            var connection = new FakeConnection();
            var log = new RecordingLog();
            var dispatcher = new PacketDispatcher(log);
            var session = new NetworkSession(connection, dispatcher, log);
            var flow = new LoginFlow(new RecordingAccountSender(), new RecordingConnector(), 1);
            var sessionSender = new SessionSender(session);
            var world = new WorldEntry(flow.Progress, sessionSender);
            SessionHandlers.Register(dispatcher, world, new ServerRequests(sessionSender),
                new GameSession(flow.Progress, sessionSender, new RecordingConnector(), new FakeClock()));
            flow.Start("127.0.0.1", 2593, new LoginCredentials("player", "secret"));
            flow.OnConnected();
            flow.OnShardList(new[] { new ShardEntry(0, "Shard", 0, 0, "127.0.0.1") });
            flow.SelectShard(0);
            flow.OnRedirect(new ServerRedirect("127.0.0.1", 2593, 5));
            flow.OnConnected();
            flow.Characters.OnCharacterList(new[] { new CharacterSlot(0, "AQuaruss") }, new StartingCity[0], 0);
            flow.Characters.Select(0);
            return (flow, world, connection, dispatcher, new GameClient(session, new FakeClock()));
        }

        private static InboundEvent Packet(byte[] bytes) => InboundEvent.FromPacket(bytes, bytes.Length, new CountingArrayPool());
    }
}
