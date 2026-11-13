using System;
using System.Buffers.Binary;
using TheVerge.Network.Framing;

namespace TheVerge.Network.Session
{
    /// <summary>
    /// Исходящие пакеты за тик (AGENTS.md §2.3, исходящее, шаги 3–4). Отправитель берёт место (<see cref="Reserve"/>),
    /// пишет пакет через PacketWriter и подтверждает длину (<see cref="Commit"/>). Каждый пакет сверяется с матрицей
    /// клиент → сервер: неизвестный номер или длина не по матрице и не по заголовку — ошибка кода клиента, пакет
    /// не попадает в отправку. В конце тика всё уходит одним вызовом (NetworkSession.Flush).
    /// </summary>
    public sealed class OutgoingBuffer
    {
        private const int InitialSize = 4 * 1024;

        private byte[] _buffer = new byte[InitialSize];
        private int _length;
        private int _reserved;
        private int[] _ends = new int[16];
        private int _count;
        private int _rawLength;

        public ReadOnlySpan<byte> Pending => new ReadOnlySpan<byte>(_buffer, 0, _length);

        /// <summary>Сколько пакетов записано за тик. Границы нужны логу пакетов: он пишет каждый пакет своей строкой.</summary>
        public int Count => _count;

        public ReadOnlySpan<byte> PacketAt(int index)
        {
            if (index < 0 || index >= _count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var start = index == 0 ? _rawLength : _ends[index - 1];
            return new ReadOnlySpan<byte>(_buffer, start, _ends[index] - start);
        }

        /// <summary>Место под пакет не длиннее <paramref name="maxLength"/>. Годно до следующего вызова.</summary>
        public Span<byte> Reserve(int maxLength)
        {
            if (maxLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxLength));
            }

            EnsureCapacity(_length + maxLength);
            _reserved = maxLength;
            return _buffer.AsSpan(_length, maxLength);
        }

        /// <summary>
        /// Байты, которые не пакет и не сверяются с матрицей: 4 байта ключа — seed игрового соединения перед 0x91
        /// (NetState.cs, AwaitingSeed). Сервер ждёт их первыми, поэтому только в начале тика, до пакетов. В
        /// <see cref="Count"/> и <see cref="PacketAt"/> они не входят, в <see cref="Pending"/> — входят.
        /// </summary>
        public void WriteRaw(ReadOnlySpan<byte> bytes)
        {
            if (_length > 0)
            {
                throw new InvalidOperationException("Сырые байты (ключ игрового соединения) — только первыми в тике, до пакетов.");
            }

            EnsureCapacity(bytes.Length);
            bytes.CopyTo(_buffer);
            _length = bytes.Length;
            _rawLength = bytes.Length;
        }

        public void Commit(int length)
        {
            if (length < 0 || length > _reserved)
            {
                throw new InvalidOperationException("Пакет длиной " + length + " байт не помещается в зарезервированные " + _reserved + ".");
            }

            Validate(new ReadOnlySpan<byte>(_buffer, _length, length));
            _length += length;
            _reserved = 0;
            if (_count == _ends.Length)
            {
                Array.Resize(ref _ends, _ends.Length * 2);
            }

            _ends[_count++] = _length;
        }

        public void Clear()
        {
            _length = 0;
            _reserved = 0;
            _count = 0;
            _rawLength = 0;
        }

        private static void Validate(ReadOnlySpan<byte> packet)
        {
            if (packet.IsEmpty)
            {
                throw new InvalidOperationException("Пустой пакет.");
            }

            var info = PacketTable.Get(PacketDirection.ClientToServer, packet[0]);
            if (!info.IsKnown)
            {
                throw new InvalidOperationException("Сервер не принимает пакет 0x" + packet[0].ToString("X2") + ": его нет в docs/protocol/packets.csv.");
            }

            var expected = ExpectedLength(packet, info);
            if (expected != packet.Length)
            {
                throw new InvalidOperationException("Пакет 0x" + packet[0].ToString("X2") + " " + info.Name + ": записано " +
                                                    packet.Length + " байт, а должно быть " + expected + ".");
            }
        }

        private static int ExpectedLength(ReadOnlySpan<byte> packet, PacketInfo info)
        {
            if (!info.IsVariable)
            {
                return info.FixedLength;
            }

            return packet.Length < PacketInfo.VariableHeaderLength
                ? PacketInfo.VariableHeaderLength
                : BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(PacketInfo.LengthOffset));
        }

        private void EnsureCapacity(int needed)
        {
            if (needed <= _buffer.Length)
            {
                return;
            }

            var grown = new byte[Math.Max(_buffer.Length * 2, needed)];
            Buffer.BlockCopy(_buffer, 0, grown, 0, _length);
            _buffer = grown;
        }
    }
}
