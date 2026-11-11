using System;

namespace TheVerge.Network.IO
{
    /// <summary>
    /// Пакет короче, чем нужно для чтения поля, или поле не влезает в буфер записи. Контролируемая ошибка вместо
    /// падения и мусора (PLAN Стадия 1, п. 1): обработчик пакета известной длины ловит её, пишет в лог и пропускает
    /// пакет (AGENTS.md §3.5). Позиция чтения или записи при этом не сдвигается.
    /// </summary>
    public sealed class PacketOutOfRangeException : Exception
    {
        public PacketOutOfRangeException(string message) : base(message)
        {
        }

        internal static PacketOutOfRangeException Create(int position, long needed, int remaining) =>
            new PacketOutOfRangeException(
                "Пакет короче, чем нужно: позиция " + position + ", нужно байт " + needed + ", осталось " + remaining + ".");
    }
}
