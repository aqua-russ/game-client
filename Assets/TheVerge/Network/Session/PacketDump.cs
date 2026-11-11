using System;

namespace TheVerge.Network.Session
{
    /// <summary>Байты пакета для лога: шестнадцатеричные пары через пробел («02 AB 00»).</summary>
    public static class PacketDump
    {
        private const string Digits = "0123456789ABCDEF";

        public static string ToHex(ReadOnlySpan<byte> bytes)
        {
            if (bytes.IsEmpty)
            {
                return string.Empty;
            }

            var chars = new char[bytes.Length * 3 - 1];
            for (var i = 0; i < bytes.Length; i++)
            {
                var at = i * 3;
                chars[at] = Digits[bytes[i] >> 4];
                chars[at + 1] = Digits[bytes[i] & 0x0F];
                if (at + 2 < chars.Length)
                {
                    chars[at + 2] = ' ';
                }
            }

            return new string(chars);
        }
    }
}
