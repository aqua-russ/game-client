using System;
using System.Buffers.Binary;
using TheVerge.Core.Session;

namespace TheVerge.Network.Framing
{
    /// <summary>
    /// Собирает целые пакеты из кусков TCP любого размера по <see cref="PacketTable"/> (PLAN Стадия 1, п. 2): длина
    /// постоянная или 2 байта после номера; у пакетов с подкомандой (0xBF, 0xF0 от сервера) подкоманда проверяется
    /// по матрице. Неизвестный номер или подкоманда и длина меньше заголовка — <see cref="FrameStatus.Error"/> с дампом:
    /// длину не угадываем, дальше поток не режется (AGENTS.md §3.5). Работает в фоновом потоке транспорта.
    /// </summary>
    public sealed class PacketFramer
    {
        // Вмещает два самых длинных пакета: полный буфер всегда содержит целый пакет, нарезка не застрянет.
        private const int Capacity = 2 * PacketInfo.MaxLength;

        private readonly PacketDirection _direction;
        private readonly byte[] _buffer = new byte[Capacity];
        private int _start;
        private int _end;
        private FramingError _error;

        public PacketFramer(PacketDirection direction)
        {
            _direction = direction;
        }

        /// <summary>Причина и дамп, когда <see cref="Next"/> вернул <see cref="FrameStatus.Error"/>.</summary>
        public FramingError Error => _error;

        public bool IsFailed => _error.Kind != FramingErrorKind.None;

        /// <summary>
        /// Добавляет байты потока. Возвращает, сколько принято: если места нет, остаток подаётся снова после того,
        /// как готовые пакеты забраны через <see cref="Next"/>. После ошибки не принимает ничего.
        /// </summary>
        public int Append(ReadOnlySpan<byte> data)
        {
            if (IsFailed)
            {
                return 0;
            }

            if (Capacity - _end < data.Length)
            {
                Compact();
            }

            var count = Math.Min(data.Length, Capacity - _end);
            data.Slice(0, count).CopyTo(_buffer.AsSpan(_end));
            _end += count;
            return count;
        }

        /// <summary>
        /// Следующий целый пакет. <paramref name="packet"/> указывает во внутренний буфер и годен до следующего вызова
        /// <see cref="Append"/> или <see cref="Next"/>: кому пакет нужен дольше — копирует.
        /// </summary>
        public FrameStatus Next(out ReadOnlySpan<byte> packet)
        {
            packet = default;
            if (IsFailed)
            {
                return FrameStatus.Error;
            }

            var available = new ReadOnlySpan<byte>(_buffer, _start, _end - _start);
            var status = Measure(available, out var length);
            if (status != FrameStatus.Packet)
            {
                return status;
            }

            packet = available.Slice(0, length);
            _start += length;
            return FrameStatus.Packet;
        }

        private FrameStatus Measure(ReadOnlySpan<byte> available, out int length)
        {
            length = 0;
            if (available.IsEmpty)
            {
                return FrameStatus.NeedMore;
            }

            var id = available[0];
            var info = PacketTable.Get(_direction, id);
            if (!info.IsKnown)
            {
                return Fail(FramingErrorKind.UnknownPacket, id, 0, 0, Head(available));
            }

            var layout = PacketTable.GetSubCommandLayout(_direction, id);
            if (!TryReadLength(available, info, out length))
            {
                return FrameStatus.NeedMore;
            }

            if (length < MinLength(info, layout))
            {
                return Fail(FramingErrorKind.BadLength, id, 0, length, Head(available));
            }

            if (available.Length < length)
            {
                return FrameStatus.NeedMore;
            }

            return CheckSubCommand(available.Slice(0, length), layout);
        }

        private static bool TryReadLength(ReadOnlySpan<byte> available, PacketInfo info, out int length)
        {
            if (!info.IsVariable)
            {
                length = info.FixedLength;
                return true;
            }

            if (available.Length < PacketInfo.VariableHeaderLength)
            {
                length = 0;
                return false;
            }

            length = BinaryPrimitives.ReadUInt16BigEndian(available.Slice(PacketInfo.LengthOffset));
            return true;
        }

        private static int MinLength(PacketInfo info, SubCommandLayout layout) =>
            Math.Max(info.IsVariable ? PacketInfo.VariableHeaderLength : 0, layout.MinPacketLength);

        private FrameStatus CheckSubCommand(ReadOnlySpan<byte> packet, SubCommandLayout layout)
        {
            if (!layout.HasSubCommand)
            {
                return FrameStatus.Packet;
            }

            var subCommand = layout.Read(packet);
            if (PacketTable.Get(_direction, packet[0], subCommand).IsKnown)
            {
                return FrameStatus.Packet;
            }

            return Fail(FramingErrorKind.UnknownSubCommand, packet[0], subCommand, packet.Length, packet);
        }

        private FrameStatus Fail(FramingErrorKind kind, byte id, ushort subCommand, int declaredLength, ReadOnlySpan<byte> dump)
        {
            _error = new FramingError(kind, id, subCommand, declaredLength, dump.ToArray());
            return FrameStatus.Error;
        }

        private static ReadOnlySpan<byte> Head(ReadOnlySpan<byte> available) =>
            available.Slice(0, Math.Min(available.Length, ClientLimits.ErrorDumpBytes));

        private void Compact()
        {
            var count = _end - _start;
            Buffer.BlockCopy(_buffer, _start, _buffer, 0, count);
            _start = 0;
            _end = count;
        }
    }
}
