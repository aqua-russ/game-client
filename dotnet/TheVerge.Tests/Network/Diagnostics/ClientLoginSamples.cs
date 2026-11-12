using TheVerge.Network.IO;

namespace TheVerge.Tests.Network.Diagnostics
{
    /// <summary>
    /// Пакеты входа клиент → сервер, собранные в том порядке, в каком их читает сервер
    /// (Projects/UOContent/Network/Packets/IncomingAccountPackets.cs: AccountLogin, GameLogin).
    /// </summary>
    internal static class ClientLoginSamples
    {
        public const string Username = "admin";
        public const string Password = "s3cret!";

        /// <summary>0x80, 62 байта: логин Latin-1 (30), пароль Latin-1 (30), последний байт сервер не читает.</summary>
        public static byte[] AccountLogin()
        {
            var packet = new byte[62];
            var writer = new PacketWriter(packet);
            writer.Write((byte)0x80);
            writer.WriteLatin1(Username, 30);
            writer.WriteLatin1(Password, 30);
            writer.WriteZeros(writer.Remaining);
            return packet;
        }

        /// <summary>0x91, 65 байт: ключ (ReadInt32), логин Latin-1 (30), пароль Latin-1 (30).</summary>
        public static byte[] GameLogin(uint authId)
        {
            var packet = new byte[65];
            var writer = new PacketWriter(packet);
            writer.Write((byte)0x91);
            writer.Write(authId);
            writer.WriteLatin1(Username, 30);
            writer.WriteLatin1(Password, 30);
            return packet;
        }
    }
}
