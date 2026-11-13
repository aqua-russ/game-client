using TheVerge.Core.Session;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Account
{
    /// <summary>
    /// 0x86 Character List Update: новый список слотов после удаления. Сервер: Projects/Server/Network/Packets/
    /// OutgoingAccountPackets.cs, SendCharacterListUpdate — byte число слотов, на слот имя UTF-8 30 байт (E1) и 30 байт
    /// пустого пароля.
    /// </summary>
    public sealed class CharacterListUpdateHandler
    {
        public const byte PacketId = 0x86;

        private const int NameLength = 30;
        private const int PasswordLength = 30;

        private readonly LoginProgress _progress;
        private readonly CharacterSelection _characters;

        public CharacterListUpdateHandler(LoginProgress progress, CharacterSelection characters)
        {
            _progress = progress;
            _characters = characters;
        }

        public void Handle(ref PacketReader reader)
        {
            var count = reader.Remaining > 0 ? reader.ReadByte() : -1;
            if (count < 0 || reader.Remaining != count * (NameLength + PasswordLength))
            {
                _progress.FailProtocol("0x86: слотов " + count + ", а данных " + reader.Remaining + " байт");
                return;
            }

            var slots = new CharacterSlot[count];
            for (var i = 0; i < count; i++)
            {
                slots[i] = new CharacterSlot(i, reader.ReadUtf8(NameLength));
                reader.Skip(PasswordLength);
            }

            _characters.OnCharacterListUpdate(slots);
        }
    }
}
