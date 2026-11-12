using NUnit.Framework;
using TheVerge.Network.Framing;
using TheVerge.Network.Packets.Account;

namespace TheVerge.Tests.Network.Packets.Account
{
    /// <summary>
    /// Где в пакетах входа лежат логин и пароль — по разбору сервера (Projects/UOContent/Network/Packets/
    /// IncomingAccountPackets.cs): AccountLogin читает ReadLatin1Safe(30) логин, затем ReadLatin1Safe(30) пароль сразу
    /// после номера; GameLogin — ReadInt32 ключ, затем те же два поля. Длины пакетов — Register(0x80, …, 62) и
    /// Register(0x91, …, 65).
    /// </summary>
    public sealed class LoginCredentialsLayoutTests
    {
        private const int ServerCredentialField = 30;
        private const int ServerAuthIdSize = 4;

        [Test]
        public void AccountLogin_UsernameThenPasswordAfterId()
        {
            Assert.That(AccountLoginPacket.PacketId, Is.EqualTo(0x80));
            Assert.That(AccountLoginPacket.CredentialLength, Is.EqualTo(ServerCredentialField));
            Assert.That(AccountLoginPacket.UsernameOffset, Is.EqualTo(PacketInfo.IdLength));
            Assert.That(AccountLoginPacket.PasswordOffset, Is.EqualTo(PacketInfo.IdLength + ServerCredentialField));
            var length = PacketTable.Get(PacketDirection.ClientToServer, AccountLoginPacket.PacketId).FixedLength;
            Assert.That(AccountLoginPacket.PasswordOffset + ServerCredentialField, Is.LessThanOrEqualTo(length));
        }

        [Test]
        public void GameLogin_AuthIdThenUsernameThenPassword()
        {
            Assert.That(GameLoginPacket.PacketId, Is.EqualTo(0x91));
            Assert.That(GameLoginPacket.CredentialLength, Is.EqualTo(ServerCredentialField));
            Assert.That(GameLoginPacket.UsernameOffset, Is.EqualTo(PacketInfo.IdLength + ServerAuthIdSize));
            Assert.That(GameLoginPacket.PasswordOffset, Is.EqualTo(GameLoginPacket.UsernameOffset + ServerCredentialField));
            var length = PacketTable.Get(PacketDirection.ClientToServer, GameLoginPacket.PacketId).FixedLength;
            Assert.That(GameLoginPacket.PasswordOffset + ServerCredentialField, Is.EqualTo(length));
        }
    }
}
