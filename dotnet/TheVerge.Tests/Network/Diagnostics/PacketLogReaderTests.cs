using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Framing;
using TheVerge.Network.Transport;
using TheVerge.Tests.Core.Session;
using TheVerge.Tests.Network.Framing;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Network.Diagnostics
{
    /// <summary>
    /// Чтение лога пакетов обратно (PLAN Стадия 1, пп. 3, 12, 13): записанная сессия читается без потерь — те же байты,
    /// время, направление, пометки и события в том же порядке. Испорченная строка — ошибка с номером строки.
    /// </summary>
    public sealed class PacketLogReaderTests
    {
        private FakeClock _clock = new FakeClock();
        private RecordingLog _log = new RecordingLog();

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock { NowMs = 500 };
            _log = new RecordingLog();
        }

        [Test]
        public void LoginSession_ReadBackExactly()
        {
            var expected = new List<PacketLogEntry>();
            var text = Write(writer =>
            {
                Connected(writer, 3, expected);
                var time = 10L;
                foreach (var packet in ServerPacketSamples.LoginStream())
                {
                    Packet(writer, time += 7, PacketDirection.ServerToClient, packet, true, expected);
                }

                Packet(writer, time += 1, PacketDirection.ClientToServer, new byte[] { 0x73, 0x07 }, true, expected);
                Packet(writer, time += 1, PacketDirection.ClientToServer, new byte[] { 0x73, 0x08 }, false, expected);
                Closed(writer, time + 40, ConnectionCloseReason.ServerClosed, "закрыто сервером", expected);
            });

            AssertEntries(text, expected);
        }

        [Test]
        public void MaskedPassword_ReadBackAsStarsWithFlag()
        {
            var text = Write(writer => writer.Packet(PacketDirection.ClientToServer, ClientLoginSamples.AccountLogin(), true));

            var entry = ReadAll(text)[0];

            Assert.That(entry.IsMasked, Is.True);
            Assert.That(entry.Packet.AsSpan(31, 30).ToArray(), Is.All.EqualTo((byte)'*'));
            Assert.That(entry.Packet.AsSpan(0, 31).ToArray(), Is.EqualTo(ClientLoginSamples.AccountLogin().AsSpan(0, 31).ToArray()));
        }

        [Test]
        public void RandomPackets_ReadBackExactly()
        {
            var random = new Random(12);
            var expected = new List<PacketLogEntry>();
            var text = Write(writer =>
            {
                for (var i = 0; i < 2000; i++)
                {
                    var packet = new byte[random.Next(1, 300)];
                    random.NextBytes(packet);
                    var direction = random.Next(2) == 0 ? PacketDirection.ServerToClient : PacketDirection.ClientToServer;
                    if (direction == PacketDirection.ClientToServer && (packet[0] == 0x80 || packet[0] == 0x91))
                    {
                        packet[0] = 0x81;
                    }

                    Packet(writer, i * 3, direction, packet, random.Next(4) != 0, expected);
                }
            });

            AssertEntries(text, expected);
        }

        [Test]
        public void CommentsAndBlankLines_Skipped_TruncationNoticed()
        {
            var text = "#TheVerge packet log v1\n#started\t2026-11-12\n\n5\tS>C\t0x73\t-\tPingAck\t2\t-\t73 07\n#truncated\t5\n";

            var reader = new PacketLogReader(new StringReader(text));
            var entries = ReadAll(reader);

            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].Packet, Is.EqualTo(new byte[] { 0x73, 0x07 }));
            Assert.That(reader.IsTruncated, Is.True);
        }

        [Test]
        public void WindowsLineEndings_ReadTheSame()
        {
            var text = "#TheVerge packet log v1\r\n5\tS>C\t0x73\t-\tPingAck\t2\t-\t73 07\r\n";

            Assert.That(ReadAll(text)[0].Packet, Is.EqualTo(new byte[] { 0x73, 0x07 }));
        }

        [TestCase("", 1)]
        [TestCase("TheVerge packet log v1\n", 1)]
        [TestCase("#TheVerge packet log v2\n", 1)]
        [TestCase("#TheVerge packet log v1\n5\tS>C\t0x73\t-\tPingAck\t2\t-\n", 2)]
        [TestCase("#TheVerge packet log v1\nx\tS>C\t0x73\t-\tPingAck\t2\t-\t73 07\n", 2)]
        [TestCase("#TheVerge packet log v1\n-5\tS>C\t0x73\t-\tPingAck\t2\t-\t73 07\n", 2)]
        [TestCase("#TheVerge packet log v1\n5\tS<C\t0x73\t-\tPingAck\t2\t-\t73 07\n", 2)]
        [TestCase("#TheVerge packet log v1\n5\tS>C\t0x74\t-\tPingAck\t2\t-\t73 07\n", 2)]
        [TestCase("#TheVerge packet log v1\n5\tS>C\t0x73\t-\tPingAck\t3\t-\t73 07\n", 2)]
        [TestCase("#TheVerge packet log v1\n5\tS>C\t0x73\t-\tPingAck\t2\t-\t73 0\n", 2)]
        [TestCase("#TheVerge packet log v1\n5\tS>C\t0x73\t-\tPingAck\t2\t-\t73 0G\n", 2)]
        [TestCase("#TheVerge packet log v1\n5\tS>C\t0x73\t-\tPingAck\t2\t-\t73  07\n", 2)]
        [TestCase("#TheVerge packet log v1\n5\tS>C\t0x73\t-\tPingAck\t0\t-\t\n", 2)]
        [TestCase("#TheVerge packet log v1\n5\tS>C\t0x73\t-\tPingAck\t2\tsecret\t73 07\n", 2)]
        [TestCase("#TheVerge packet log v1\n5\tS>C\t0xBF\t0x0009\tMapChange\t6\t-\tBF 00 06 00 08 00\n", 2)]
        [TestCase("#TheVerge packet log v1\n5\tS>C\t0xBF\t-\tMapChange\t6\t-\tBF 00 06 00 08 00\n", 2)]
        [TestCase("#TheVerge packet log v1\n5\tS>C\t0x73\t0x07\tPingAck\t2\t-\t73 07\n", 2)]
        [TestCase("#TheVerge packet log v1\n5\tCONNECTED\textra\n", 2)]
        [TestCase("#TheVerge packet log v1\n5\tCLOSED\tNoSuchReason\t\n", 2)]
        [TestCase("#TheVerge packet log v1\n5\tCLOSED\t99\t\n", 2)]
        [TestCase("#TheVerge packet log v1\n5\tCLOSED\tServerClosed\n", 2)]
        public void BrokenLine_ErrorWithLineNumber(string text, int line)
        {
            var reader = new PacketLogReader(new StringReader(text));

            var error = Assert.Throws<PacketLogFormatException>(new Action(() => ReadAll(reader)));

            Assert.That(error!.LineNumber, Is.EqualTo(line));
            Assert.That(error.Message, Does.Contain("строка " + line));
        }

        private string Write(Action<PacketLogWriter> body)
        {
            var output = new StringWriter();
            var writer = new PacketLogWriter(output, _clock, _log, long.MaxValue);
            body(writer);
            writer.Flush();
            Assert.That(_log.Entries, Is.Empty);
            return output.ToString();
        }

        private void Packet(PacketLogWriter writer, long time, PacketDirection direction, byte[] packet, bool sent,
            List<PacketLogEntry> expected)
        {
            _clock.NowMs = 500 + time;
            writer.Packet(direction, packet, sent);
            expected.Add(PacketLogEntry.ForPacket(time, direction, packet, false, sent));
        }

        private void Connected(PacketLogWriter writer, long time, List<PacketLogEntry> expected)
        {
            _clock.NowMs = 500 + time;
            writer.Connected();
            expected.Add(PacketLogEntry.ForConnected(time));
        }

        private void Closed(PacketLogWriter writer, long time, ConnectionCloseReason reason, string detail, List<PacketLogEntry> expected)
        {
            _clock.NowMs = 500 + time;
            writer.Closed(reason, detail);
            expected.Add(PacketLogEntry.ForClosed(time, reason, detail));
        }

        private static void AssertEntries(string text, List<PacketLogEntry> expected)
        {
            var actual = ReadAll(text);
            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            for (var i = 0; i < expected.Count; i++)
            {
                Assert.That(actual[i].Kind, Is.EqualTo(expected[i].Kind), "запись " + i);
                Assert.That(actual[i].TimeMs, Is.EqualTo(expected[i].TimeMs), "запись " + i);
                Assert.That(actual[i].Direction, Is.EqualTo(expected[i].Direction), "запись " + i);
                Assert.That(actual[i].Packet, Is.EqualTo(expected[i].Packet), "запись " + i);
                Assert.That(actual[i].IsMasked, Is.EqualTo(expected[i].IsMasked), "запись " + i);
                Assert.That(actual[i].IsSent, Is.EqualTo(expected[i].IsSent), "запись " + i);
                Assert.That(actual[i].CloseReason, Is.EqualTo(expected[i].CloseReason), "запись " + i);
                Assert.That(actual[i].Detail, Is.EqualTo(expected[i].Detail), "запись " + i);
            }
        }

        private static List<PacketLogEntry> ReadAll(string text) => ReadAll(new PacketLogReader(new StringReader(text)));

        private static List<PacketLogEntry> ReadAll(PacketLogReader reader)
        {
            var entries = new List<PacketLogEntry>();
            while (reader.TryRead(out var entry))
            {
                entries.Add(entry);
            }

            return entries;
        }
    }
}
