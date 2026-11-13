using System;
using System.Linq;
using NUnit.Framework;
using TheVerge.Core.Session;
using TheVerge.Network.Framing;
using TheVerge.Network.IO;
using TheVerge.Network.Packets.Account;
using TheVerge.Network.Session;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Network.Packets.Account
{
    /// <summary>
    /// Вход на игровой сервер. Сервер читает первые 4 байта игрового соединения как seed big-endian
    /// (Projects/Server/Network/NetState/NetState.cs, AwaitingSeed: (id &lt;&lt; 24) | …), затем 0x91 (IncomingAccountPackets.cs,
    /// GameLogin: ReadInt32 ключ — должен совпасть с seed, ReadLatin1Safe(30) логин, ReadLatin1Safe(30) пароль; длина 65)
    /// и сразу сжимает ответ (state.CompressionEnabled = true).
    /// </summary>
    public sealed class GameLoginPacketTests
    {
        private const int AuthKey = unchecked((int)0xA1B2C3D4);

        [Test]
        public void GameLogin_KeyBigEndianThenLatin1Fields_65Bytes()
        {
            var bytes = PacketBytes.Write((ref PacketWriter w) => GameLoginPacket.Write(ref w, AuthKey, new LoginCredentials("Jörg", "pw")));

            var expected = new byte[65];
            expected[0] = 0x91;
            expected[1] = 0xA1;
            expected[2] = 0xB2;
            expected[3] = 0xC3;
            expected[4] = 0xD4;
            expected[5] = (byte)'J';
            expected[6] = 0xF6;
            expected[7] = (byte)'r';
            expected[8] = (byte)'g';
            expected[35] = (byte)'p';
            expected[36] = (byte)'w';
            Assert.That(bytes, Is.EqualTo(expected));
            Assert.That(bytes.Length, Is.EqualTo(PacketTable.Get(PacketDirection.ClientToServer, 0x91).FixedLength));
        }

        [Test]
        public void SendGameLogin_RawKeyThen0x91_OneSend_CompressionAlreadyOn()
        {
            var connection = new FakeConnection();
            var session = new NetworkSession(connection, new PacketDispatcher(new RecordingLog()), new RecordingLog());

            new AccountSender(session).SendGameLogin(AuthKey, new LoginCredentials("player", "secret"));
            session.Flush();

            Assert.That(connection.Sent.Count, Is.EqualTo(1));
            var sent = connection.Sent[0];
            Assert.That(sent.Length, Is.EqualTo(4 + 65));
            Assert.That(sent.Take(4), Is.EqualTo(new byte[] { 0xA1, 0xB2, 0xC3, 0xD4 }), "seed сервер читает big-endian");
            Assert.That(sent[4], Is.EqualTo(0x91));
            Assert.That(sent.Skip(5).Take(4), Is.EqualTo(sent.Take(4)), "ключ в 0x91 совпадает с seed");
            Assert.That(connection.CompressionAtSend[0], Is.True, "ответ на 0x91 уже сжат");
        }

        [Test]
        public void PacketLog_GameLoginOneLine_PasswordMasked_SeedNotAPacketLine()
        {
            var output = new System.IO.StringWriter();
            var log = new RecordingLog();
            var session = new NetworkSession(new FakeConnection(), new PacketDispatcher(log), log)
            {
                PacketLog = new TheVerge.Network.Diagnostics.PacketLogWriter(output, new TheVerge.Tests.Core.Session.FakeClock(), log, long.MaxValue),
            };

            new AccountSender(session).SendGameLogin(AuthKey, new LoginCredentials("player", "s3cretPw"));
            session.Flush();

            var lines = output.ToString().Split('\n').Where(l => l.Contains("C>S")).ToArray();
            Assert.That(lines.Length, Is.EqualTo(1));
            Assert.That(lines[0], Does.Contain("0x91").And.Contain("masked"));
            Assert.That(output.ToString(), Does.Not.Contain("73 33 63 72 65 74 50 77"), "пароль s3cretPw байтами");
        }

        [Test]
        public void RawKey_NotAPacket_OnlyGameLoginCounted()
        {
            var buffer = new OutgoingBuffer();

            buffer.WriteRaw(new byte[] { 0xA1, 0xB2, 0xC3, 0xD4 });
            var writer = new PacketWriter(buffer.Reserve(65));
            GameLoginPacket.Write(ref writer, AuthKey, new LoginCredentials("player", "secret"));
            buffer.Commit(writer.Position);

            Assert.That(buffer.Count, Is.EqualTo(1));
            Assert.That(buffer.PacketAt(0)[0], Is.EqualTo(0x91));
            Assert.That(buffer.PacketAt(0).Length, Is.EqualTo(65));
            Assert.That(buffer.Pending.Length, Is.EqualTo(69));
        }

        [Test]
        public void RawKey_AfterPacketInSameTick_Refused()
        {
            // Ключ — seed соединения: сервер ждёт его первым байтом, после пакета он сломал бы поток.
            var buffer = new OutgoingBuffer();
            var writer = new PacketWriter(buffer.Reserve(3));
            SelectServerPacket.Write(ref writer, 0);
            buffer.Commit(writer.Position);

            Assert.Throws<InvalidOperationException>(new Action(() => buffer.WriteRaw(new byte[] { 1, 2, 3, 4 })));
        }
    }
}
