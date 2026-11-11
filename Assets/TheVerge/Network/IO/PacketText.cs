using System;
using System.Text;

namespace TheVerge.Network.IO
{
    /// <summary>
    /// Кодировки строк протокола — те же, что у сервера (Projects/Server/Text/TextEncoding.cs, без «safe»-фильтра):
    /// ASCII (байт больше 0x7F читается как «?»), Latin-1 (байт = символ), UTF-8 (битые байты — U+FFFD) и UTF-16
    /// big-endian. Latin-1 сделан вручную: в netstandard2.1 нет Encoding.Latin1, а кодовые страницы в Unity
    /// зависят от платформы. Символ, которого нет в кодировке, при записи становится «?», как у сервера.
    /// </summary>
    internal static class PacketText
    {
        private const char Unrepresentable = '?';
        private const char MaxLatin1 = 'ÿ';

        private static readonly Encoding Utf8 = new UTF8Encoding(false, false);
        private static readonly Encoding Utf16BigEndian = new UnicodeEncoding(true, false, false);

        public static string DecodeAscii(ReadOnlySpan<byte> bytes) => Encoding.ASCII.GetString(bytes);

        public static string DecodeUtf8(ReadOnlySpan<byte> bytes) => Utf8.GetString(bytes);

        public static string DecodeUtf16BigEndian(ReadOnlySpan<byte> bytes) => Utf16BigEndian.GetString(bytes);

        public static string DecodeLatin1(ReadOnlySpan<byte> bytes)
        {
            if (bytes.IsEmpty)
            {
                return string.Empty;
            }

            Span<char> chars = bytes.Length <= 256 ? stackalloc char[bytes.Length] : new char[bytes.Length];
            for (var i = 0; i < bytes.Length; i++)
            {
                chars[i] = (char)bytes[i];
            }

            return new string(chars);
        }

        public static void EncodeAscii(ReadOnlySpan<char> value, Span<byte> destination) =>
            Encoding.ASCII.GetBytes(value, destination);

        public static void EncodeLatin1(ReadOnlySpan<char> value, Span<byte> destination)
        {
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                destination[i] = (byte)(c <= MaxLatin1 ? c : Unrepresentable);
            }
        }

        public static void EncodeUtf8(ReadOnlySpan<char> value, Span<byte> destination) => Utf8.GetBytes(value, destination);

        public static void EncodeUtf16BigEndian(ReadOnlySpan<char> value, Span<byte> destination) =>
            Utf16BigEndian.GetBytes(value, destination);

        public static int Utf8ByteCount(ReadOnlySpan<char> value) => Utf8.GetByteCount(value);

        /// <summary>
        /// Сколько первых символов строки влезает в поле UTF-8 из <paramref name="maxBytes"/> байт. Как у сервера
        /// (SpanWriter.Write с fixedLength): символы отбрасываются с конца, буква не разрывается посередине.
        /// В отличие от сервера, не разрывается и суррогатная пара.
        /// </summary>
        public static int Utf8FitLength(string value, int maxBytes)
        {
            var length = WholeChars(value, Math.Min(value.Length, maxBytes));
            while (length > 0 && Utf8.GetByteCount(value.AsSpan(0, length)) > maxBytes)
            {
                length = WholeChars(value, length - 1);
            }

            return length;
        }

        /// <summary>Не даёт отрезать вторую половину суррогатной пары: такая строка закодировалась бы как U+FFFD.</summary>
        public static int WholeChars(string value, int length) =>
            length > 0 && char.IsHighSurrogate(value[length - 1]) ? length - 1 : length;
    }
}
