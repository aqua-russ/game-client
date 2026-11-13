using TheVerge.Core.Session;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Account
{
    /// <summary>
    /// 0x85 Delete Result: отказ в удалении персонажа. Сервер: Projects/Server/Network/Packets/OutgoingAccountPackets.cs,
    /// SendCharacterDeleteResult — байт причины (DeleteResultType); следом сервер шлёт список 0x86
    /// (UOContent/Accounting/AccountHandler.cs, DeleteRequest).
    /// </summary>
    public sealed class DeleteResultHandler
    {
        public const byte PacketId = 0x85;

        private readonly CharacterSelection _characters;

        public DeleteResultHandler(CharacterSelection characters)
        {
            _characters = characters;
        }

        public void Handle(ref PacketReader reader) => _characters.OnDeleteRejected((CharacterDeleteResult)reader.ReadByte());
    }
}
