using TheVerge.Core.Session;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Chat
{
    /// <summary>
    /// 0xAD Unicode Speech: речь игрока. Сервер: Projects/UOContent/Network/Packets/IncomingMessagePackets.cs, UnicodeSpeech —
    /// byte тип, short цвет, short шрифт, ReadAscii(4) язык, затем UTF-16 big-endian до нуля (ReadBigUniSafe). Тип 0
    /// (Regular) без флага Encoded: ключевые слова не шлём — для GM-команд и простой речи они не нужны.
    /// </summary>
    public static class UnicodeSpeechPacket
    {
        public const byte PacketId = 0xAD;

        private const byte TypeRegular = 0;

        /// <summary>Шрифт: сервер его не читает; 3 — обычный шрифт классического клиента.</summary>
        private const short Font = 3;

        private const int LanguageLength = 4;

        /// <summary>Номер, длина, тип, цвет, шрифт, язык и ноль в конце текста.</summary>
        private const int FixedLength = 1 + 2 + 1 + 2 + 2 + LanguageLength + 2;

        public static int LengthOf(string text) => FixedLength + 2 * text.Length;

        public static void Write(ref PacketWriter writer, string text, ushort hue)
        {
            writer.Write(PacketId);
            writer.Write((ushort)LengthOf(text));
            writer.Write(TypeRegular);
            writer.Write(hue);
            writer.Write(Font);
            writer.WriteAscii(ClientLimits.Language, LanguageLength);
            writer.WriteUtf16BigEndianNullTerminated(text);
        }
    }
}
