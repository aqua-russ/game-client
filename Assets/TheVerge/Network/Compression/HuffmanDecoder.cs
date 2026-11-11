using System;

namespace TheVerge.Network.Compression
{
    /// <summary>
    /// Потоковая распаковка Huffman. Сервер сжимает всё, что шлёт на игровой сокет после 0x91
    /// (Projects/UOContent/Network/Packets/IncomingAccountPackets.cs: CompressionEnabled; сжатие —
    /// Projects/Server/Network/NetworkCompression.cs). Каждый пакет сжат отдельно и кончается кодом конца блока,
    /// после него биты добиты нулями до целого байта. Клиент ничего не сжимает.
    /// TCP режет поток где угодно, поэтому разбор продолжается между вызовами с того же бита (PLAN Стадия 1, п. 3).
    /// </summary>
    public sealed class HuffmanDecoder
    {
        private const int HighBit = 0x80;

        private int _currentByte;
        private int _bitMask;   // какой бит текущего байта читать следующим; 0 — байт разобран
        private int _node;      // узел дерева посреди кода; 0 — между символами

        /// <summary>
        /// Распаковывает <paramref name="source"/> в <paramref name="destination"/>. Останавливается, когда вход кончился
        /// или выход заполнен, — всегда на границе символа, поэтому ничего не теряется. Возвращает число записанных байтов.
        /// <paramref name="consumed"/> — сколько байтов входа забрано; неразобранные биты последнего из них
        /// хранятся внутри и разбираются следующим вызовом.
        /// </summary>
        public int Decode(ReadOnlySpan<byte> source, Span<byte> destination, out int consumed)
        {
            consumed = 0;
            int written = 0;
            while (written < destination.Length)
            {
                if (_bitMask == 0 && !TryTakeByte(source, ref consumed))
                {
                    break;
                }

                _node = HuffmanTree.Next(_node, (_currentByte & _bitMask) != 0 ? 1 : 0);
                _bitMask >>= 1;
                if (_node >= 0)
                {
                    continue;
                }

                int symbol = ~_node;
                _node = 0;
                if (symbol == HuffmanTree.EndOfBlock)
                {
                    _bitMask = 0; // остаток байта — выравнивание нулями до конца пакета
                    continue;
                }

                destination[written++] = (byte)symbol;
            }

            return written;
        }

        private bool TryTakeByte(ReadOnlySpan<byte> source, ref int consumed)
        {
            if (consumed == source.Length)
            {
                return false;
            }

            _currentByte = source[consumed++];
            _bitMask = HighBit;
            return true;
        }
    }
}
