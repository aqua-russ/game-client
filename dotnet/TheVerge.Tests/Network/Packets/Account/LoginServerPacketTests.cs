using System;
using NUnit.Framework;
using TheVerge.Core.Session;
using TheVerge.Network.Framing;
using TheVerge.Network.IO;
using TheVerge.Network.Packets.Account;
using TheVerge.Tests.Network.Packets;

namespace TheVerge.Tests.Network.Packets.Account
{
    /// <summary>
    /// Байты пакетов логин-сервера — по разбору сервера (Projects/UOContent/Network/Packets/IncomingAccountPackets.cs).
    /// 0xEF, LoginServerSeed: ReadInt32 seed, затем ReadInt32 ×4 — версия (major, minor, revision, patch); длина 21
    /// (Register(0xEF, …, 21)). 0x80, AccountLogin: ReadLatin1Safe(30) логин, ReadLatin1Safe(30) пароль; длина 62
    /// (Register(0x80, …, 62)) — последний байт сервер не читает.
    /// </summary>
    public sealed class LoginServerPacketTests
    {
        [Test]
        public void LoginSeed_SeedThenVersion_BigEndian_21Bytes()
        {
            var bytes = PacketBytes.Write((ref PacketWriter w) => LoginSeedPacket.Write(ref w, 0x0A0B0C0D, new ClientVersion(7, 0, 95, 0)));

            Assert.That(bytes, Is.EqualTo(new byte[]
            {
                0xEF, 0x0A, 0x0B, 0x0C, 0x0D,
                0x00, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x5F, 0x00, 0x00, 0x00, 0x00,
            }));
            Assert.That(bytes.Length, Is.EqualTo(MatrixLength(0xEF)));
        }

        [Test]
        public void AccountLogin_Latin1Fields_62Bytes()
        {
            var bytes = PacketBytes.Write((ref PacketWriter w) => AccountLoginPacket.Write(ref w, new LoginCredentials("Jörg", "pw")));

            var expected = new byte[62];
            expected[0] = 0x80;
            expected[1] = (byte)'J';
            expected[2] = 0xF6;
            expected[3] = (byte)'r';
            expected[4] = (byte)'g';
            expected[31] = (byte)'p';
            expected[32] = (byte)'w';
            Assert.That(bytes, Is.EqualTo(expected));
            Assert.That(bytes.Length, Is.EqualTo(MatrixLength(0x80)));
        }

        [Test]
        public void AccountLogin_FullLengthFields_ServerReadsSameValues()
        {
            var login = new string('L', 30);
            var password = new string('P', 30);

            var bytes = PacketBytes.Write((ref PacketWriter w) => AccountLoginPacket.Write(ref w, new LoginCredentials(login, password)));

            var reader = new PacketReader(bytes.AsSpan(1));
            Assert.That(reader.ReadLatin1(30), Is.EqualTo(login));
            Assert.That(reader.ReadLatin1(30), Is.EqualTo(password));
        }

        private static int MatrixLength(byte id) => PacketTable.Get(PacketDirection.ClientToServer, id).FixedLength;
    }
}
