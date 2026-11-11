using System;
using System.Buffers.Binary;

namespace TheVerge.Network.IO
{
    /// <summary>
    /// Запись полей пакета в заданный буфер. Порядок байт — big-endian (AGENTS.md §3.5). Строки пишутся как у сервера
    /// (Projects/Server/Buffers/SpanWriter.cs, Write(value, encoding, fixedLength)): поле фиксированной длины — строка,
    /// обрезанная по целым символам, остаток — нули; «…NullTerminated» — строка и ноль (у UTF-16 — два нулевых байта).
    /// Поле, которое не влезает в буфер, — <see cref="PacketOutOfRangeException"/> до записи: буфер и позиция не меняются.
    /// </summary>
    public ref struct PacketWriter
    {
        private const int Utf16CharSize = 2;

        private readonly Span<byte> _buffer;
        private int _position;

        public PacketWriter(Span<byte> buffer)
        {
            _buffer = buffer;
            _position = 0;
        }

        public int Position => _position;

        public int Remaining => _buffer.Length - _position;

        /// <summary>Записанная часть буфера — готовые байты пакета.</summary>
        public ReadOnlySpan<byte> Written => _buffer.Slice(0, _position);

        public void Write(byte value) => Take(1)[0] = value;

        public void Write(sbyte value) => Take(1)[0] = (byte)value;

        public void Write(bool value) => Take(1)[0] = value ? (byte)1 : (byte)0;

        public void Write(ushort value) => BinaryPrimitives.WriteUInt16BigEndian(Take(2), value);

        public void Write(short value) => BinaryPrimitives.WriteInt16BigEndian(Take(2), value);

        public void Write(uint value) => BinaryPrimitives.WriteUInt32BigEndian(Take(4), value);

        public void Write(int value) => BinaryPrimitives.WriteInt32BigEndian(Take(4), value);

        public void Write(ReadOnlySpan<byte> bytes) => bytes.CopyTo(Take(bytes.Length));

        public void WriteZeros(int count) => Take(count).Clear();

        public void WriteAscii(string value, int fixedLength)
        {
            var field = TakeCleared(fixedLength, 1);
            PacketText.EncodeAscii(value.AsSpan(0, Math.Min(value.Length, fixedLength)), field);
        }

        public void WriteAsciiNullTerminated(string value)
        {
            var field = TakeCleared(value.Length + 1, 1);
            PacketText.EncodeAscii(value.AsSpan(), field);
        }

        public void WriteLatin1(string value, int fixedLength)
        {
            var field = TakeCleared(fixedLength, 1);
            PacketText.EncodeLatin1(value.AsSpan(0, Math.Min(value.Length, fixedLength)), field);
        }

        /// <summary>Имена в UTF-8 (наше расширение E1): поле <paramref name="fixedLength"/> байт, буква не разрывается.</summary>
        public void WriteUtf8(string value, int fixedLength)
        {
            var field = TakeCleared(fixedLength, 1);
            PacketText.EncodeUtf8(value.AsSpan(0, PacketText.Utf8FitLength(value, fixedLength)), field);
        }

        /// <summary>Поле из <paramref name="fixedChars"/> символов UTF-16 big-endian (по 2 байта).</summary>
        public void WriteUtf16BigEndian(string value, int fixedChars)
        {
            var field = TakeCleared(fixedChars, Utf16CharSize);
            var length = PacketText.WholeChars(value, Math.Min(value.Length, fixedChars));
            PacketText.EncodeUtf16BigEndian(value.AsSpan(0, length), field);
        }

        public void WriteUtf16BigEndianNullTerminated(string value)
        {
            var field = TakeCleared(value.Length + 1, Utf16CharSize);
            PacketText.EncodeUtf16BigEndian(value.AsSpan(), field);
        }

        private Span<byte> TakeCleared(int count, int unitSize)
        {
            EnsureAvailable(count, unitSize);
            var field = Take(count * unitSize);
            field.Clear();
            return field;
        }

        private Span<byte> Take(int count)
        {
            EnsureAvailable(count, 1);
            var span = _buffer.Slice(_position, count);
            _position += count;
            return span;
        }

        private void EnsureAvailable(int count, int unitSize)
        {
            if (count < 0 || count > Remaining / unitSize)
            {
                throw PacketOutOfRangeException.Create(_position, (long)count * unitSize, Remaining);
            }
        }
    }
}
