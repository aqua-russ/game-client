using System;

namespace TheVerge.Network.Framing
{
    /// <summary>Что сломалось в потоке и байты для дампа.</summary>
    public readonly struct FramingError
    {
        public FramingError(FramingErrorKind kind, byte id, ushort subCommand, int declaredLength, byte[] dump)
        {
            Kind = kind;
            Id = id;
            SubCommand = subCommand;
            DeclaredLength = declaredLength;
            _dump = dump;
        }

        private readonly byte[]? _dump;

        public FramingErrorKind Kind { get; }

        public byte Id { get; }

        /// <summary>Подкоманда при <see cref="FramingErrorKind.UnknownSubCommand"/>, иначе 0.</summary>
        public ushort SubCommand { get; }

        /// <summary>Длина из заголовка пакета; 0, если она не прочитана.</summary>
        public int DeclaredLength { get; }

        /// <summary>
        /// Весь пакет, если его длина известна, иначе до ClientLimits.ErrorDumpBytes байт с места ошибки.
        /// Пустой массив, если ошибки нет.
        /// </summary>
        public byte[] Dump => _dump ?? Array.Empty<byte>();
    }
}
