using TheVerge.Core.Session;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Account
{
    /// <summary>
    /// 0xB9 Supported Features: флаги возможностей сервера. Сервер: Projects/Server/Network/Packets/
    /// OutgoingAccountPackets.cs, SendSupportedFeature — для клиентов 6.0.14.2+ (ExtendedSupportedFeatures, наш 7.0.95.0)
    /// uint флаги, длина 5.
    /// </summary>
    public sealed class SupportedFeaturesHandler
    {
        public const byte PacketId = 0xB9;

        private readonly CharacterSelection _characters;

        public SupportedFeaturesHandler(CharacterSelection characters)
        {
            _characters = characters;
        }

        public void Handle(ref PacketReader reader) => _characters.OnSupportedFeatures(reader.ReadUInt32());
    }
}
