using TheVerge.Core.Ports;
using TheVerge.Network.IO;
using TheVerge.Network.Session;

namespace TheVerge.Network.Packets.Chat
{
    /// <summary>Порт <see cref="ISpeechSender"/> (ADR 0008 п. 1): речь в исходящие тика.</summary>
    public sealed class SpeechSender : ISpeechSender
    {
        private readonly NetworkSession _session;

        public SpeechSender(NetworkSession session)
        {
            _session = session;
        }

        public void SendSpeech(string text, ushort hue)
        {
            var writer = new PacketWriter(_session.Outgoing.Reserve(UnicodeSpeechPacket.LengthOf(text)));
            UnicodeSpeechPacket.Write(ref writer, text, hue);
            _session.Outgoing.Commit(writer.Position);
        }
    }
}
