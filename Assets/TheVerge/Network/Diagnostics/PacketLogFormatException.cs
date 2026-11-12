using System;

namespace TheVerge.Network.Diagnostics
{
    /// <summary>Строку лога пакетов нельзя разобрать: файл испорчен или записан другой версией формата.</summary>
    public sealed class PacketLogFormatException : Exception
    {
        public PacketLogFormatException(int lineNumber, string problem)
            : base("Лог пакетов, строка " + lineNumber + ": " + problem)
        {
            LineNumber = lineNumber;
        }

        /// <summary>Номер строки в файле, с 1.</summary>
        public int LineNumber { get; }
    }
}
