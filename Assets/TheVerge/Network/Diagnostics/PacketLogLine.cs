using System;

namespace TheVerge.Network.Diagnostics
{
    /// <summary>
    /// Сборка одной строки лога в переиспользуемом буфере символов: запись строки не выделяет память, кроме роста
    /// буфера под самый длинный пакет.
    /// </summary>
    internal sealed class PacketLogLine
    {
        private const string Digits = "0123456789ABCDEF";

        private char[] _chars = new char[256];

        public int Length { get; private set; }

        public char[] Chars => _chars;

        public void Clear() => Length = 0;

        public void Append(char value)
        {
            Ensure(1);
            _chars[Length++] = value;
        }

        /// <summary>Значение поля и табуляция после него.</summary>
        public void AppendField(string value)
        {
            Append(value);
            Append(PacketLogFormat.Separator);
        }

        public void Append(string value)
        {
            Ensure(value.Length);
            value.CopyTo(0, _chars, Length, value.Length);
            Length += value.Length;
        }

        /// <summary>Текст со стороны (причина закрытия, заголовок): табуляции и переводы строк — пробелы, строка одна.</summary>
        public void AppendText(string value)
        {
            Ensure(value.Length);
            foreach (var c in value)
            {
                _chars[Length++] = char.IsControl(c) ? ' ' : c;
            }
        }

        public void AppendNumber(long value)
        {
            var digits = 1;
            for (var rest = value / 10; rest > 0; rest /= 10)
            {
                digits++;
            }

            Ensure(digits);
            for (var at = Length + digits - 1; at >= Length; at--)
            {
                _chars[at] = (char)('0' + (int)(value % 10));
                value /= 10;
            }

            Length += digits;
        }

        /// <summary>«0x» и число в <paramref name="bytes"/> байт шестнадцатеричными цифрами.</summary>
        public void AppendHexNumber(int value, int bytes)
        {
            Append("0x");
            for (var shift = (bytes - 1) * 8; shift >= 0; shift -= 8)
            {
                AppendHexByte((byte)(value >> shift));
            }
        }

        /// <summary>Байты парами через пробел; байты [maskStart, maskStart + maskLength) — как «*».</summary>
        public void AppendBytes(ReadOnlySpan<byte> bytes, int maskStart, int maskLength)
        {
            Ensure(bytes.Length * 3);
            for (var i = 0; i < bytes.Length; i++)
            {
                if (i > 0)
                {
                    _chars[Length++] = ' ';
                }

                var masked = i >= maskStart && i < maskStart + maskLength;
                AppendHexByte(masked ? PacketLogSecrets.MaskByte : bytes[i]);
            }
        }

        private void AppendHexByte(byte value)
        {
            Ensure(2);
            _chars[Length++] = Digits[value >> 4];
            _chars[Length++] = Digits[value & 0x0F];
        }

        private void Ensure(int more)
        {
            if (Length + more <= _chars.Length)
            {
                return;
            }

            Array.Resize(ref _chars, Math.Max(_chars.Length * 2, Length + more));
        }
    }
}
