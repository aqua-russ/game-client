using TheVerge.Core.Ports;
using TheVerge.Core.Session;
using TheVerge.Network.Framing;
using TheVerge.Network.IO;
using TheVerge.Network.Session;

namespace TheVerge.Network.Packets.Account
{
    /// <summary>
    /// Порт <see cref="IAccountSender"/> (ADR 0008 п. 1): пишет пакеты входа в исходящие тика сессии; в сокет они уходят
    /// в конце тика одним вызовом.
    /// </summary>
    public sealed class AccountSender : IAccountSender
    {
        private readonly NetworkSession _session;

        public AccountSender(NetworkSession session)
        {
            _session = session;
        }

        public void SendLoginSeed(int seed, ClientVersion version)
        {
            var writer = Begin(LoginSeedPacket.PacketId);
            LoginSeedPacket.Write(ref writer, seed, version);
            _session.Outgoing.Commit(writer.Position);
        }

        public void SendAccountLogin(LoginCredentials credentials)
        {
            var writer = Begin(AccountLoginPacket.PacketId);
            AccountLoginPacket.Write(ref writer, credentials);
            _session.Outgoing.Commit(writer.Position);
        }

        public void SendSelectShard(int index)
        {
            var writer = Begin(SelectServerPacket.PacketId);
            SelectServerPacket.Write(ref writer, index);
            _session.Outgoing.Commit(writer.Position);
        }

        private PacketWriter Begin(byte id) =>
            new PacketWriter(_session.Outgoing.Reserve(PacketTable.Get(PacketDirection.ClientToServer, id).FixedLength));
    }
}
