using System;
using TheVerge.Network.Framing;
using TheVerge.Network.Packets.Account;

namespace TheVerge.Network.Diagnostics
{
    /// <summary>
    /// Какие байты пакета не пишутся в лог открытым текстом: пароль в 0x80 и 0x91. Скрывается всё поле, а не только
    /// буквы пароля, чтобы по логу не узнать даже его длину. Раскладка полей — из классов пакетов входа.
    /// </summary>
    internal static class PacketLogSecrets
    {
        /// <summary>Чем заменяется скрытый байт: «*».</summary>
        public const byte MaskByte = 0x2A;

        public static bool TryGetMasked(PacketDirection direction, ReadOnlySpan<byte> packet, out int start, out int length)
        {
            start = 0;
            length = 0;
            if (direction != PacketDirection.ClientToServer)
            {
                return false;
            }

            switch (packet[0])
            {
                case AccountLoginPacket.PacketId:
                    return Clip(packet.Length, AccountLoginPacket.PasswordOffset, AccountLoginPacket.CredentialLength, out start, out length);
                case GameLoginPacket.PacketId:
                    return Clip(packet.Length, GameLoginPacket.PasswordOffset, GameLoginPacket.CredentialLength, out start, out length);
                default:
                    return false;
            }
        }

        // Пакет короче положенного (ошибка кода клиента) всё равно не должен выдать пароль: скрываем, что есть.
        private static bool Clip(int packetLength, int offset, int fieldLength, out int start, out int length)
        {
            start = Math.Min(offset, packetLength);
            length = Math.Min(fieldLength, packetLength - start);
            return length > 0;
        }
    }
}
