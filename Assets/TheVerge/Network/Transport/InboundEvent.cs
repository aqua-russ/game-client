using System;
using System.Buffers;
using TheVerge.Network.Framing;

namespace TheVerge.Network.Transport
{
    /// <summary>
    /// Элемент очереди от фонового потока сокета к главному (AGENTS.md §2.3): целый пакет в буфере из пула, ошибка
    /// нарезки или закрытие соединения. Буфер пакета после обработки возвращается в пул через <see cref="Release"/>.
    /// </summary>
    public readonly struct InboundEvent
    {
        private readonly byte[]? _buffer;
        private readonly ArrayPool<byte>? _pool;
        private readonly string? _reason;

        private InboundEvent(InboundKind kind, byte[]? buffer, int length, ArrayPool<byte>? pool, FramingError error, string? reason)
        {
            Kind = kind;
            _buffer = buffer;
            Length = length;
            _pool = pool;
            Error = error;
            _reason = reason;
        }

        public InboundKind Kind { get; }

        /// <summary>Длина пакета; 0 у ошибки и закрытия.</summary>
        public int Length { get; }

        /// <summary>Байты пакета. Годны до <see cref="Release"/>.</summary>
        public ReadOnlySpan<byte> Packet => _buffer == null ? ReadOnlySpan<byte>.Empty : new ReadOnlySpan<byte>(_buffer, 0, Length);

        public FramingError Error { get; }

        /// <summary>Почему закрыто соединение (для лога).</summary>
        public string Reason => _reason ?? string.Empty;

        public static InboundEvent FromPacket(byte[] buffer, int length, ArrayPool<byte> pool) =>
            new InboundEvent(InboundKind.Packet, buffer, length, pool, default, null);

        public static InboundEvent FramingFailed(FramingError error) =>
            new InboundEvent(InboundKind.FramingFailed, null, 0, null, error, null);

        public static InboundEvent Closed(string reason) =>
            new InboundEvent(InboundKind.Closed, null, 0, null, default, reason);

        /// <summary>Вернуть буфер пакета в пул. Вызывается ровно один раз, после обработки пакета.</summary>
        public void Release()
        {
            if (_buffer != null)
            {
                _pool?.Return(_buffer);
            }
        }
    }
}
