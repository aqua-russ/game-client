using TheVerge.Core.Ports;
using TheVerge.Core.Session;
using TheVerge.Network.Framing;
using TheVerge.Network.IO;
using NetworkSession = TheVerge.Network.Session.NetworkSession;

namespace TheVerge.Network.Packets.Session
{
    /// <summary>Порт <see cref="ISessionSender"/> (ADR 0008 п. 1): служебные пакеты игровой сессии в исходящие тика.</summary>
    public sealed class SessionSender : ISessionSender
    {
        private readonly NetworkSession _session;
        private ClientVersion _version;
        private string? _versionText;

        public SessionSender(NetworkSession session)
        {
            _session = session;
        }

        public void SendClientVersion(ClientVersion version)
        {
            // Запрос версии приходит и во время ходьбы (замер RTT): строка версии собирается один раз, без мусора на ответ.
            if (_versionText == null || !Same(version, _version))
            {
                _version = version;
                _versionText = version.ToString();
            }

            var writer = new PacketWriter(_session.Outgoing.Reserve(ClientVersionPacket.LengthOf(_versionText)));
            ClientVersionPacket.Write(ref writer, _versionText);
            _session.Outgoing.Commit(writer.Position);
        }

        public void SendLanguage(string language)
        {
            var writer = new PacketWriter(_session.Outgoing.Reserve(LanguagePacket.Length));
            LanguagePacket.Write(ref writer, language);
            _session.Outgoing.Commit(writer.Position);
        }

        public void SendPing(byte sequence)
        {
            var writer = Begin(PingPacket.PacketId);
            PingPacket.Write(ref writer, sequence);
            _session.Outgoing.Commit(writer.Position);
        }

        public void SendLogout()
        {
            var writer = Begin(LogoutPacket.PacketId);
            LogoutPacket.Write(ref writer);
            _session.Outgoing.Commit(writer.Position);
        }

        public void SendViewRange(int range)
        {
            var writer = Begin(ViewRangePacket.PacketId);
            ViewRangePacket.Write(ref writer, range);
            _session.Outgoing.Commit(writer.Position);
        }

        private static bool Same(ClientVersion a, ClientVersion b) =>
            a.Major == b.Major && a.Minor == b.Minor && a.Revision == b.Revision && a.Patch == b.Patch;

        private PacketWriter Begin(byte fixedLengthId) =>
            new PacketWriter(_session.Outgoing.Reserve(PacketTable.Get(PacketDirection.ClientToServer, fixedLengthId).FixedLength));
    }
}
