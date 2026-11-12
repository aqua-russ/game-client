using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Framing;
using TheVerge.Network.Session;
using TheVerge.Network.Transport;
using TheVerge.Tests.Core.Session;
using TheVerge.Tests.Network.Framing;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Network.Diagnostics
{
    /// <summary>
    /// Лог пакетов (PLAN Стадия 1, п. 12): один пакет — одна строка «время, направление, номер, подкоманда, имя, длина,
    /// пометки, байты», поля через табуляцию. Время — мс от начала лога. Имя — из PacketTable (docs/protocol/packets.csv).
    /// Пароль в 0x80 и 0x91 заменяется звёздочками только в записи.
    /// </summary>
    public sealed class PacketLogWriterTests
    {
        private const long LargeLimit = 1024 * 1024;

        private StringWriter _output = new StringWriter();
        private FakeClock _clock = new FakeClock();
        private RecordingLog _log = new RecordingLog();

        [SetUp]
        public void SetUp()
        {
            _output = new StringWriter();
            _clock = new FakeClock { NowMs = 1000 };
            _log = new RecordingLog();
        }

        [Test]
        public void NewLog_FirstLineIsFormatVersion()
        {
            NewWriter();

            Assert.That(Lines(), Is.EqualTo(new[] { "#TheVerge packet log v1" }));
        }

        [Test]
        public void Header_IsCommentWithKeyAndValue()
        {
            var writer = NewWriter();

            writer.Header("started", "2026-11-12T17:41:38Z");

            Assert.That(Lines().Last(), Is.EqualTo("#started\t2026-11-12T17:41:38Z"));
        }

        [Test]
        public void IncomingPacket_TimeFromStart_DirectionIdNameLengthHex()
        {
            var writer = NewWriter();
            _clock.NowMs = 1250;

            writer.Packet(PacketDirection.ServerToClient, ServerPacketSamples.PingAck(), true);

            Assert.That(Lines().Last(), Is.EqualTo("250\tS>C\t0x73\t-\tPingAck\t2\t-\t73 07"));
        }

        [Test]
        public void OutgoingPacket_ClientToServerNameFromItsMatrix()
        {
            var writer = NewWriter();

            writer.Packet(PacketDirection.ClientToServer, new byte[] { 0x73, 0x07 }, true);

            Assert.That(Lines().Last(), Is.EqualTo("0\tC>S\t0x73\t-\tPing\t2\t-\t73 07"));
        }

        [Test]
        public void TwoByteSubCommand_WrittenWithItsName()
        {
            var writer = NewWriter();

            writer.Packet(PacketDirection.ServerToClient, ServerPacketSamples.MapChange(), true);

            Assert.That(Lines().Last(), Is.EqualTo("0\tS>C\t0xBF\t0x0008\tMapChange\t6\t-\tBF 00 06 00 08 00"));
        }

        [Test]
        public void OneByteSubCommand_WrittenWithItsName()
        {
            var writer = NewWriter();
            var packet = ServerPacketSamples.PartyLocations();

            writer.Packet(PacketDirection.ServerToClient, packet, true);

            Assert.That(Lines().Last(), Is.EqualTo("0\tS>C\t0xF0\t0x01\tPartyLocations\t17\t-\t" + PacketDump.ToHex(packet)));
        }

        [Test]
        public void PacketNotInMatrix_NameIsQuestionMark()
        {
            var writer = NewWriter();

            writer.Packet(PacketDirection.ServerToClient, new byte[] { 0x01, 0x02 }, true);

            Assert.That(Lines().Last(), Is.EqualTo("0\tS>C\t0x01\t-\t?\t2\t-\t01 02"));
        }

        [Test]
        public void NotSent_MarkedUnsent()
        {
            var writer = NewWriter();

            writer.Packet(PacketDirection.ClientToServer, new byte[] { 0x73, 0x01 }, false);

            Assert.That(Lines().Last(), Is.EqualTo("0\tC>S\t0x73\t-\tPing\t2\tunsent\t73 01"));
        }

        [Test]
        public void AccountLogin_PasswordFieldStars_UsernameKept_PacketUnchanged()
        {
            var writer = NewWriter();
            var packet = ClientLoginSamples.AccountLogin();
            var original = packet.ToArray();

            writer.Packet(PacketDirection.ClientToServer, packet, true);

            var expected = original.ToArray();
            for (var i = 31; i < 61; i++)
            {
                expected[i] = (byte)'*';
            }

            Assert.That(Lines().Last(), Is.EqualTo("0\tC>S\t0x80\t-\tAccountLogin\t62\tmasked\t" + PacketDump.ToHex(expected)));
            Assert.That(packet, Is.EqualTo(original));
            Assert.That(_output.ToString(), Does.Not.Contain(PacketDump.ToHex(Encoding.Latin1.GetBytes(ClientLoginSamples.Password))));
            Assert.That(_output.ToString(), Does.Contain(PacketDump.ToHex(Encoding.Latin1.GetBytes(ClientLoginSamples.Username))));
        }

        [Test]
        public void GameLogin_PasswordFieldStars_AuthIdAndUsernameKept()
        {
            var writer = NewWriter();
            var packet = ClientLoginSamples.GameLogin(0x12345678);

            writer.Packet(PacketDirection.ClientToServer, packet, true);

            var expected = packet.ToArray();
            for (var i = 35; i < 65; i++)
            {
                expected[i] = (byte)'*';
            }

            Assert.That(Lines().Last(), Is.EqualTo("0\tC>S\t0x91\t-\tGameLogin\t65\tmasked\t" + PacketDump.ToHex(expected)));
        }

        [Test]
        public void MaskedAndUnsent_BothFlags()
        {
            var writer = NewWriter();

            writer.Packet(PacketDirection.ClientToServer, ClientLoginSamples.AccountLogin(), false);

            Assert.That(Lines().Last().Split('\t')[6], Is.EqualTo("masked,unsent"));
        }

        [Test]
        public void EmptyPacket_IsCodeError()
        {
            var writer = NewWriter();

            Assert.Throws<ArgumentException>(new Action(() => writer.Packet(PacketDirection.ServerToClient, ReadOnlySpan<byte>.Empty, true)));
        }

        [Test]
        public void ConnectedAndClosed_EventLines_DetailOnOneLine()
        {
            var writer = NewWriter();
            _clock.NowMs = 1005;
            writer.Connected();
            _clock.NowMs = 1010;

            writer.Closed(ConnectionCloseReason.ServerClosed, "сервер\tзакрыл\r\nсоединение");

            Assert.That(Lines().Skip(1), Is.EqualTo(new[] { "5\tCONNECTED", "10\tCLOSED\tServerClosed\tсервер закрыл  соединение" }));
        }

        [Test]
        public void Limit_OneTruncatedLine_WarningOnce_NothingAfter()
        {
            var writer = NewWriter(64);
            _clock.NowMs = 1007;

            for (var i = 0; i < 5; i++)
            {
                writer.Packet(PacketDirection.ServerToClient, ServerPacketSamples.PingAck(), true);
            }

            Assert.That(writer.IsStopped, Is.True);
            Assert.That(Lines(), Is.EqualTo(new[] { "#TheVerge packet log v1", "7\tS>C\t0x73\t-\tPingAck\t2\t-\t73 07", "#truncated\t7" }));
            Assert.That(_log.Warnings.Count(), Is.EqualTo(1));
        }

        [Test]
        public void WriteFails_ErrorLoggedOnce_StopsWithoutThrowing()
        {
            var failing = new FailingWriter();
            var writer = new PacketLogWriter(failing, _clock, _log, LargeLimit);

            writer.Packet(PacketDirection.ServerToClient, ServerPacketSamples.PingAck(), true);
            writer.Packet(PacketDirection.ServerToClient, ServerPacketSamples.PingAck(), true);
            writer.Flush();

            Assert.That(writer.IsStopped, Is.True);
            Assert.That(_log.Errors.Count(), Is.EqualTo(1));
            Assert.That(_log.Errors.Single(), Does.Contain("диск полон"));
        }

        private PacketLogWriter NewWriter(long limit = LargeLimit) => new PacketLogWriter(_output, _clock, _log, limit);

        // Строки лога разделяются одним \n на любой ОС: файл с Mac читается так же, как из Linux.
        private string[] Lines() => _output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        private sealed class FailingWriter : TextWriter
        {
            public override Encoding Encoding => Encoding.UTF8;

            public override void Write(char value) => throw new IOException("диск полон");

            public override void Write(char[] buffer, int index, int count) => throw new IOException("диск полон");

            public override void Write(string? value) => throw new IOException("диск полон");
        }
    }
}
