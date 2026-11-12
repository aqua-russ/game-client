using System;
using TheVerge.Network.Framing;
using TheVerge.Network.Transport;

namespace TheVerge.Network.Diagnostics
{
    /// <summary>Одна запись лога пакетов, прочитанная обратно: пакет с направлением и пометками или событие соединения.</summary>
    public readonly struct PacketLogEntry
    {
        private readonly byte[]? _packet;
        private readonly string? _detail;

        private PacketLogEntry(long timeMs, PacketDirection direction, byte[] packet, bool isMasked, bool isSent)
        {
            Kind = PacketLogKind.Packet;
            TimeMs = timeMs;
            Direction = direction;
            _packet = packet;
            IsMasked = isMasked;
            IsSent = isSent;
            CloseReason = ConnectionCloseReason.None;
            _detail = null;
        }

        private PacketLogEntry(PacketLogKind kind, long timeMs, ConnectionCloseReason closeReason, string? detail)
        {
            Kind = kind;
            TimeMs = timeMs;
            Direction = default;
            _packet = null;
            IsMasked = false;
            IsSent = false;
            CloseReason = closeReason;
            _detail = detail;
        }

        public PacketLogKind Kind { get; }

        /// <summary>Мс от начала лога.</summary>
        public long TimeMs { get; }

        public PacketDirection Direction { get; }

        /// <summary>Байты пакета; пусто у событий. Если <see cref="IsMasked"/>, пароль в них заменён звёздочками.</summary>
        public byte[] Packet => _packet ?? Array.Empty<byte>();

        public bool IsMasked { get; }

        /// <summary>Пакет передан в сокет; false — отправка не удалась (соединения нет).</summary>
        public bool IsSent { get; }

        public ConnectionCloseReason CloseReason { get; }

        /// <summary>Подробности закрытия (текст ошибки сокета); пусто у остальных записей.</summary>
        public string Detail => _detail ?? string.Empty;

        public static PacketLogEntry ForPacket(long timeMs, PacketDirection direction, byte[] packet, bool isMasked, bool isSent) =>
            new PacketLogEntry(timeMs, direction, packet, isMasked, isSent);

        public static PacketLogEntry ForConnected(long timeMs) =>
            new PacketLogEntry(PacketLogKind.Connected, timeMs, ConnectionCloseReason.None, null);

        public static PacketLogEntry ForClosed(long timeMs, ConnectionCloseReason reason, string detail) =>
            new PacketLogEntry(PacketLogKind.Closed, timeMs, reason, detail);
    }
}
