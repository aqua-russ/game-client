using System;
using System.Buffers.Binary;

namespace TheVerge.Network.IO
{
    /// <summary>
    /// Чтение полей пакета. Порядок байт протокола — big-endian (AGENTS.md §3.5). Строки читаются как у сервера
    /// (Projects/Server/Buffers/SpanReader.cs): поле фиксированной длины кончается на первом нуле, но позиция сдвигается
    /// на всё поле; строка без длины — до нуля (ноль пропускается) или до конца пакета; у UTF-16 ноль — пара нулевых
    /// байтов на чётном месте. Чтение за концом — <see cref="PacketOutOfRangeException"/>, позиция не сдвигается.
    /// ref struct: читатель живёт только на время разбора и не может унести ссылку на буфер пакета из пула.
    /// </summary>
    public ref struct PacketReader
    {
        private const int Utf16CharSize = 2;

        private readonly ReadOnlySpan<byte> _buffer;
        private int _position;

        public PacketReader(ReadOnlySpan<byte> buffer)
        {
            _buffer = buffer;
            _position = 0;
        }

        public int Position => _position;

        public int Length => _buffer.Length;

        public int Remaining => _buffer.Length - _position;

        public byte ReadByte() => Take(1)[0];

        public sbyte ReadSByte() => (sbyte)Take(1)[0];

        /// <summary>Как у сервера (SpanReader.ReadBoolean): любой ненулевой байт — истина.</summary>
        public bool ReadBoolean() => Take(1)[0] != 0;

        public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16BigEndian(Take(2));

        public short ReadInt16() => BinaryPrimitives.ReadInt16BigEndian(Take(2));

        public uint ReadUInt32() => BinaryPrimitives.ReadUInt32BigEndian(Take(4));

        public int ReadInt32() => BinaryPrimitives.ReadInt32BigEndian(Take(4));

        public void ReadBytes(Span<byte> destination) => Take(destination.Length).CopyTo(destination);

        public void Skip(int count) => Take(count);

        public string ReadAscii(int fixedLength) => PacketText.DecodeAscii(TakeField(fixedLength));

        public string ReadAsciiNullTerminated() => PacketText.DecodeAscii(TakeUntilZero());

        public string ReadLatin1(int fixedLength) => PacketText.DecodeLatin1(TakeField(fixedLength));

        public string ReadLatin1NullTerminated() => PacketText.DecodeLatin1(TakeUntilZero());

        /// <summary>Имена в UTF-8 (наше расширение E1, docs/protocol/extensions.md): поле фиксированной длины в байтах.</summary>
        public string ReadUtf8(int fixedLength) => PacketText.DecodeUtf8(TakeField(fixedLength));

        /// <summary>Поле из <paramref name="fixedChars"/> символов UTF-16 big-endian (по 2 байта).</summary>
        public string ReadUtf16BigEndian(int fixedChars)
        {
            EnsureAvailable(fixedChars, Utf16CharSize);
            var field = Take(fixedChars * Utf16CharSize);
            var end = IndexOfUtf16Zero(field);
            return PacketText.DecodeUtf16BigEndian(end < 0 ? field : field.Slice(0, end));
        }

        public string ReadUtf16BigEndianNullTerminated()
        {
            // Как у сервера: нечётный последний байт в строку не входит и остаётся непрочитанным.
            var rest = _buffer.Slice(_position, Remaining - Remaining % Utf16CharSize);
            var end = IndexOfUtf16Zero(rest);
            if (end < 0)
            {
                _position += rest.Length;
                return PacketText.DecodeUtf16BigEndian(rest);
            }

            _position += end + Utf16CharSize;
            return PacketText.DecodeUtf16BigEndian(rest.Slice(0, end));
        }

        private ReadOnlySpan<byte> TakeField(int length)
        {
            var field = Take(length);
            var end = field.IndexOf((byte)0);
            return end < 0 ? field : field.Slice(0, end);
        }

        private ReadOnlySpan<byte> TakeUntilZero()
        {
            var rest = _buffer.Slice(_position);
            var end = rest.IndexOf((byte)0);
            if (end < 0)
            {
                _position = _buffer.Length;
                return rest;
            }

            _position += end + 1;
            return rest.Slice(0, end);
        }

        private ReadOnlySpan<byte> Take(int count)
        {
            EnsureAvailable(count, 1);
            var span = _buffer.Slice(_position, count);
            _position += count;
            return span;
        }

        /// <summary>Проверка до умножения: огромное число символов не переполнит int и не обойдёт проверку.</summary>
        private void EnsureAvailable(int count, int unitSize)
        {
            if (count < 0 || count > Remaining / unitSize)
            {
                throw PacketOutOfRangeException.Create(_position, (long)count * unitSize, Remaining);
            }
        }

        private static int IndexOfUtf16Zero(ReadOnlySpan<byte> bytes)
        {
            for (var i = 0; i + 1 < bytes.Length; i += Utf16CharSize)
            {
                if (bytes[i] == 0 && bytes[i + 1] == 0)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
