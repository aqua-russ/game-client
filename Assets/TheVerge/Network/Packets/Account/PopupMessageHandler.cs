using TheVerge.Core.Session;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Account
{
    /// <summary>
    /// 0x53 Popup Message: игровой сервер не пустил персонажа. Сервер: Projects/Server/Network/Packets/
    /// OutgoingAccountPackets.cs, SendPopupMessage — байт причины (PMMessage); шлёт его только PlayCharacter и
    /// CreateCharacter (IncomingAccountPackets.cs), когда другой персонаж аккаунта в мире.
    /// </summary>
    public sealed class PopupMessageHandler
    {
        public const byte PacketId = 0x53;

        private readonly LoginFlow _flow;

        public PopupMessageHandler(LoginFlow flow)
        {
            _flow = flow;
        }

        public void Handle(ref PacketReader reader) => _flow.OnCharacterRejected((CharacterRejectReason)reader.ReadByte());
    }
}
