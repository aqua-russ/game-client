using TheVerge.Core.Session;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Account
{
    /// <summary>
    /// 0xEF Login Seed: первое, что получает логин-сервер, — seed и версия клиента. Сервер:
    /// Projects/UOContent/Network/Packets/IncomingAccountPackets.cs, LoginServerSeed — ReadInt32 seed (0 — отключение),
    /// затем ReadInt32 ×4: major, minor, revision, patch. Длина 21 (Register(0xEF, …, 21)).
    /// </summary>
    public static class LoginSeedPacket
    {
        public const byte PacketId = 0xEF;

        public static void Write(ref PacketWriter writer, int seed, ClientVersion version)
        {
            writer.Write(PacketId);
            writer.Write(seed);
            writer.Write(version.Major);
            writer.Write(version.Minor);
            writer.Write(version.Revision);
            writer.Write(version.Patch);
        }
    }
}
