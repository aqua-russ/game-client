using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using TheVerge.Core.Ports;
using TheVerge.Core.Session;
using TheVerge.Network.Framing;
using TheVerge.Network.IO;
using TheVerge.Network.Session;
using TheVerge.Network.Transport;
using TheVerge.Tests.Network.Transport;

namespace TheVerge.Tests.Network.Session
{
    /// <summary>
    /// GameClient вместе с настоящей сетевой частью (PLAN Стадия 1, п. 4а, Тест): ответ обработчика уходит в том же тике
    /// одной отправкой (нужно для 0xBD, PLAN п. 10); неизвестный номер — дисконнект с дампом в лог.
    /// Запрос 0xBD — OutgoingAccountPackets.cs, SendClientVersionRequest; ответ 0xBD — номер, длина, строка версии
    /// с нулём (IncomingAccountPackets.cs, ClientVersion).
    /// </summary>
    public sealed class GameClientNetworkTests
    {
        private static readonly byte[] VersionReply =
        {
            0xBD, 0x00, 0x0C, 0x37, 0x2E, 0x30, 0x2E, 0x39, 0x35, 0x2E, 0x30, 0x00,
        };

        [Test]
        public void VersionRequest_AnsweredInSameTick_OneSend()
        {
            var connection = new FakeConnection();
            var log = new RecordingLog();
            var session = NewSessionAnsweringVersion(connection, log);
            var clock = new StopwatchClock();
            var client = new GameClient(session, clock);
            connection.Push(InboundEvent.FromPacket(new byte[] { 0xBD, 0x00, 0x03 }, 3, new CountingArrayPool()));
            connection.Push(InboundEvent.FromPacket(new byte[] { 0xBD, 0x00, 0x03 }, 3, new CountingArrayPool()));

            client.Tick(clock.NowMs);

            Assert.That(connection.Sent.Count, Is.EqualTo(1));
            Assert.That(connection.Sent[0], Is.EqualTo(VersionReply.Concat(VersionReply)));
        }

        [Test]
        public void UnknownPacket_TickDisconnectsWithDump()
        {
            var connection = new FakeConnection();
            var log = new RecordingLog();
            var session = new NetworkSession(connection, new PacketDispatcher(log), log);
            var clock = new StopwatchClock();
            var error = new FramingError(FramingErrorKind.UnknownPacket, 0x02, 0, 0, new byte[] { 0x02, 0xFF });
            connection.Push(InboundEvent.FramingFailed(error));

            new GameClient(session, clock).Tick(clock.NowMs);

            Assert.That(connection.Disposed, Is.True);
            Assert.That(log.Errors.Single(), Does.Contain("0x02").And.Contain("02 FF"));
        }

        [Test]
        public void RealSocket_ServerAsksVersion_ReceivesAnswer()
        {
            using (var server = new LocalServer())
            {
                var log = new RecordingLog();
                var connection = new TcpConnection(new CountingArrayPool(), ClientLimits.ConnectTimeoutMs, ClientLimits.InboundQueueBytes);
                using (var session = NewSessionAnsweringVersion(connection, log))
                {
                    var clock = new StopwatchClock();
                    var client = new GameClient(session, clock);
                    connection.Connect("127.0.0.1", server.Port, CancellationToken.None);
                    server.Accept();
                    server.Client.Send(new byte[] { 0xBD, 0x00, 0x03 });

                    var answer = new byte[VersionReply.Length];
                    var received = 0;
                    var watch = Stopwatch.StartNew();
                    server.Client.Blocking = false;
                    while (received < answer.Length && watch.ElapsedMilliseconds < 5000)
                    {
                        client.Tick(clock.NowMs);
                        received += ReadAvailable(server, answer, received);
                        Thread.Sleep(1);
                    }

                    Assert.That(answer, Is.EqualTo(VersionReply));
                    Assert.That(log.Errors, Is.Empty);
                }
            }
        }

        private static NetworkSession NewSessionAnsweringVersion(IConnection connection, ILog log)
        {
            var dispatcher = new PacketDispatcher(log);
            NetworkSession? session = null;
            dispatcher.Register(0xBD, (ref PacketReader reader) =>
            {
                var writer = new PacketWriter(session!.Outgoing.Reserve(64));
                writer.Write((byte)0xBD);
                writer.Write((ushort)12);
                writer.WriteAsciiNullTerminated("7.0.95.0");
                session.Outgoing.Commit(writer.Position);
            });
            session = new NetworkSession(connection, dispatcher, log);
            return session;
        }

        private static int ReadAvailable(LocalServer server, byte[] answer, int offset)
        {
            if (server.Client.Available == 0)
            {
                return 0;
            }

            return server.Client.Receive(answer, offset, answer.Length - offset, System.Net.Sockets.SocketFlags.None);
        }

        private sealed class StopwatchClock : IClock
        {
            private readonly Stopwatch _watch = Stopwatch.StartNew();

            public long NowMs => _watch.ElapsedMilliseconds;
        }
    }
}
