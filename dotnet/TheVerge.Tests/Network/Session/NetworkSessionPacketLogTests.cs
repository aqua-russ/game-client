using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using TheVerge.Core.Session;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Framing;
using TheVerge.Network.IO;
using TheVerge.Network.Session;
using TheVerge.Network.Transport;
using TheVerge.Tests.Core.Session;
using TheVerge.Tests.Network.Diagnostics;
using TheVerge.Tests.Network.Framing;
using TheVerge.Tests.Network.Transport;

namespace TheVerge.Tests.Network.Session
{
    /// <summary>
    /// Лог пакетов в сетевой сессии (PLAN Стадия 1, п. 12): входящие пишутся после нарезки, до обработки; исходящие —
    /// в конце тика, когда передаются в сокет, каждый пакет своей строкой; подключение и закрытие — тоже строки.
    /// В конце тика записанное сбрасывается в файл. Без лога обработка пакетов не выделяет память (AGENTS.md §3.1).
    /// </summary>
    public sealed class NetworkSessionPacketLogTests
    {
        private FakeConnection _connection = new FakeConnection();
        private RecordingLog _log = new RecordingLog();
        private FakeClock _clock = new FakeClock();
        private CountingTextWriter _output = new CountingTextWriter();
        private CountingArrayPool _pool = new CountingArrayPool();

        [SetUp]
        public void SetUp()
        {
            _connection = new FakeConnection();
            _log = new RecordingLog();
            _clock = new FakeClock { NowMs = 100 };
            _output = new CountingTextWriter();
            _pool = new CountingArrayPool();
        }

        [Test]
        public void IncomingPackets_LoggedInOrder()
        {
            var session = NewSession(new PacketDispatcher(_log));
            Push(ServerPacketSamples.PingAck());
            Push(ServerPacketSamples.LoginComplete());

            session.ProcessNext();
            _clock.NowMs = 140;
            session.ProcessNext();

            var entries = Entries();
            Assert.That(entries.Select(e => e.Direction), Is.All.EqualTo(PacketDirection.ServerToClient));
            Assert.That(entries.Select(e => e.Packet), Is.EqualTo(new[] { ServerPacketSamples.PingAck(), ServerPacketSamples.LoginComplete() }));
            Assert.That(entries.Select(e => e.TimeMs), Is.EqualTo(new long[] { 0, 40 }));
        }

        [Test]
        public void IncomingPacket_LoggedBeforeHandlerRuns()
        {
            var dispatcher = new PacketDispatcher(_log);
            dispatcher.Register(0x73, (ref PacketReader r) => throw new InvalidOperationException("ошибка обработчика"));
            var session = NewSession(dispatcher);
            Push(ServerPacketSamples.PingAck());

            Assert.Throws<InvalidOperationException>(new Action(() => session.ProcessNext()));

            Assert.That(Entries().Single().Packet, Is.EqualTo(ServerPacketSamples.PingAck()));
        }

        [Test]
        public void OutgoingPacketsOfTick_OneLineEach_SameTime_OneSend()
        {
            var session = NewSession(new PacketDispatcher(_log));
            _clock.NowMs = 155;
            Write(session, new byte[] { 0x73, 0x01 });
            Write(session, new byte[] { 0xBD, 0x00, 0x04, 0x00 });
            Write(session, new byte[] { 0xD1, 0x00 });

            session.Flush();

            var entries = Entries();
            Assert.That(entries.Select(e => e.Packet), Is.EqualTo(new[]
            {
                new byte[] { 0x73, 0x01 }, new byte[] { 0xBD, 0x00, 0x04, 0x00 }, new byte[] { 0xD1, 0x00 },
            }));
            Assert.That(entries.Select(e => e.Direction), Is.All.EqualTo(PacketDirection.ClientToServer));
            Assert.That(entries.Select(e => e.TimeMs), Is.All.EqualTo(55));
            Assert.That(entries.Select(e => e.IsSent), Is.All.True);
            Assert.That(_connection.Sent.Count, Is.EqualTo(1));
        }

        [Test]
        public void NothingToSend_NoLines()
        {
            var session = NewSession(new PacketDispatcher(_log));

            session.Flush();

            Assert.That(Entries(), Is.Empty);
        }

        [Test]
        public void SendFails_LinesMarkedUnsent()
        {
            var session = NewSession(new PacketDispatcher(_log));
            _connection.AcceptsSend = false;
            Write(session, new byte[] { 0x73, 0x01 });

            session.Flush();

            Assert.That(Entries().Single().IsSent, Is.False);
        }

        [Test]
        public void AccountLogin_SocketGetsPassword_LogGetsStars()
        {
            var session = NewSession(new PacketDispatcher(_log));
            Write(session, ClientLoginSamples.AccountLogin());

            session.Flush();

            Assert.That(_connection.Sent.Single(), Is.EqualTo(ClientLoginSamples.AccountLogin()));
            var entry = Entries().Single();
            Assert.That(entry.IsMasked, Is.True);
            Assert.That(entry.Packet.AsSpan(31, 30).ToArray(), Is.All.EqualTo((byte)'*'));
        }

        [Test]
        public void ConnectedAndClosed_LoggedWithReason()
        {
            var session = NewSession(new PacketDispatcher(_log));
            _connection.Push(InboundEvent.Connected());
            _connection.Push(InboundEvent.Closed(ConnectionCloseReason.ServerClosed, "сброс"));

            session.ProcessNext();
            session.ProcessNext();

            var entries = Entries();
            Assert.That(entries.Select(e => e.Kind), Is.EqualTo(new[] { PacketLogKind.Connected, PacketLogKind.Closed }));
            Assert.That(entries[1].CloseReason, Is.EqualTo(ConnectionCloseReason.ServerClosed));
            Assert.That(entries[1].Detail, Is.EqualTo("сброс"));
        }

        [Test]
        public void FramingFailed_LoggedAsClosedByFraming()
        {
            var session = NewSession(new PacketDispatcher(_log));
            var error = new FramingError(FramingErrorKind.UnknownPacket, 0x02, 0, 0, new byte[] { 0x02, 0x01 });
            _connection.Push(InboundEvent.FramingFailed(error));

            session.ProcessNext();

            var entry = Entries().Single();
            Assert.That(entry.Kind, Is.EqualTo(PacketLogKind.Closed));
            Assert.That(entry.CloseReason, Is.EqualTo(ConnectionCloseReason.FramingFailed));
            Assert.That(entry.Detail, Does.Contain("0x02"));
        }

        [Test]
        public void EndOfTick_LogFlushedOnlyWhenSomethingWritten()
        {
            var session = NewSession(new PacketDispatcher(_log));
            Push(ServerPacketSamples.PingAck());
            session.ProcessNext();

            session.Flush();
            var afterFirst = _output.Flushes;
            session.Flush();

            Assert.That(afterFirst, Is.EqualTo(1));
            Assert.That(_output.Flushes, Is.EqualTo(1));
        }

        [Test]
        public void WithoutLog_HandlingAndSendingDoNotAllocate()
        {
            var dispatcher = new PacketDispatcher(_log);
            dispatcher.Register(0x73, (ref PacketReader r) => r.ReadByte());
            _connection.KeepSent = false;
            var session = new NetworkSession(_connection, dispatcher, _log);
            for (var i = 0; i < 101; i++)
            {
                Push(ServerPacketSamples.PingAck());
            }

            var ping = new byte[] { 0x73, 0x01 };
            HandleOneAndSend(session, ping);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100; i++)
            {
                HandleOneAndSend(session, ping);
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.EqualTo(0));
            Assert.That(_connection.TryReceive(out _), Is.False, "все пакеты разобраны в замере");
        }

        [Test]
        public void RealSocket_PacketsLoggedAfterFraming_WholeOrByOneByte()
        {
            var stream = ServerPacketSamples.Concat(ServerPacketSamples.LoginStream());
            using (var server = new LocalServer())
            {
                var connection = new TcpConnection(new CountingArrayPool(), ClientLimits.ConnectTimeoutMs, ClientLimits.InboundQueueBytes);
                using (var session = new NetworkSession(connection, new PacketDispatcher(_log), _log))
                {
                    session.PacketLog = new PacketLogWriter(_output, _clock, _log, long.MaxValue);
                    connection.Connect("127.0.0.1", server.Port, CancellationToken.None);
                    server.Accept();
                    server.SendInChunks(stream, 40);
                    server.SendInChunks(stream, 1);
                    var expected = ServerPacketSamples.LoginStream().Concat(ServerPacketSamples.LoginStream()).ToList();

                    var watch = Stopwatch.StartNew();
                    while (Entries().Count(e => e.Kind == PacketLogKind.Packet) < expected.Count && watch.ElapsedMilliseconds < 5000)
                    {
                        Tick(session);
                        Thread.Sleep(1);
                    }

                    var entries = Entries();
                    Assert.That(entries[0].Kind, Is.EqualTo(PacketLogKind.Connected));
                    Assert.That(entries.Skip(1).Select(e => e.Packet), Is.EqualTo(expected));
                }
            }
        }

        private NetworkSession NewSession(PacketDispatcher dispatcher) =>
            new NetworkSession(_connection, dispatcher, _log) { PacketLog = new PacketLogWriter(_output, _clock, _log, long.MaxValue) };

        // Один пакет и один ответ за тик: в замере каждый тик делает работу. Assert.That здесь нельзя: он сам выделяет память.
        private static void HandleOneAndSend(NetworkSession session, byte[] reply)
        {
            if (!session.ProcessNext())
            {
                Assert.Fail("очередь опустела раньше замера");
            }

            Write(session, reply);
            session.Flush();
        }

        private static void Tick(NetworkSession session)
        {
            while (session.ProcessNext())
            {
            }

            session.Flush();
        }

        private List<PacketLogEntry> Entries()
        {
            var reader = new PacketLogReader(new StringReader(_output.Text));
            var entries = new List<PacketLogEntry>();
            while (reader.TryRead(out var entry))
            {
                entries.Add(entry);
            }

            return entries;
        }

        private void Push(byte[] packet)
        {
            var buffer = _pool.Rent(packet.Length);
            packet.CopyTo(buffer, 0);
            _connection.Push(InboundEvent.FromPacket(buffer, packet.Length, _pool));
        }

        private static void Write(NetworkSession session, byte[] packet)
        {
            packet.CopyTo(session.Outgoing.Reserve(packet.Length));
            session.Outgoing.Commit(packet.Length);
        }

        /// <summary>Вывод в память, который считает сбросы (Flush) — как файл, куда лог сбрасывается в конце тика.</summary>
        private sealed class CountingTextWriter : StringWriter
        {
            public int Flushes { get; private set; }

            public string Text => ToString();

            public override void Flush()
            {
                Flushes++;
                base.Flush();
            }
        }
    }
}
