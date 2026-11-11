using System.Collections.Generic;
using TheVerge.Network.Compression;

namespace TheVerge.Tests.Network.Compression
{
    /// <summary>
    /// Сжатие, как у сервера (Projects/Server/Network/NetworkCompression.cs: каждый пакет сжимается отдельно, в конце —
    /// код конца блока, затем нули до целого байта), — чтобы подавать в тесты приёма настоящий сжатый поток. Коды берутся
    /// обходом дерева распаковки, а само сжатие сверено с эталонными байтами сервера (HuffmanTestEncoderTests): тест
    /// не замыкается сам на себя.
    /// </summary>
    internal static class HuffmanTestEncoder
    {
        private static readonly (int Code, int Length)[] Codes = BuildCodes();

        public static byte[] Compress(byte[] packet)
        {
            var output = new List<byte>();
            int current = 0, bits = 0;
            foreach (var symbol in packet)
            {
                Append(Codes[symbol], output, ref current, ref bits);
            }

            Append(Codes[HuffmanTree.EndOfBlock], output, ref current, ref bits);
            if (bits > 0)
            {
                output.Add((byte)(current << (8 - bits)));
            }

            return output.ToArray();
        }

        public static byte[] CompressEach(IEnumerable<byte[]> packets)
        {
            var output = new List<byte>();
            foreach (var packet in packets)
            {
                output.AddRange(Compress(packet));
            }

            return output.ToArray();
        }

        private static void Append((int Code, int Length) code, List<byte> output, ref int current, ref int bits)
        {
            for (var i = code.Length - 1; i >= 0; i--)
            {
                current = (current << 1) | ((code.Code >> i) & 1);
                if (++bits == 8)
                {
                    output.Add((byte)current);
                    current = 0;
                    bits = 0;
                }
            }
        }

        private static (int, int)[] BuildCodes()
        {
            var codes = new (int, int)[HuffmanTree.EndOfBlock + 1];
            var pending = new Stack<(int Node, int Code, int Length)>();
            pending.Push((0, 0, 0));
            while (pending.Count > 0)
            {
                var (node, code, length) = pending.Pop();
                for (var bit = 0; bit <= 1; bit++)
                {
                    var next = HuffmanTree.Next(node, bit);
                    var nextCode = (code << 1) | bit;
                    if (next < 0)
                    {
                        codes[~next] = (nextCode, length + 1);
                    }
                    else
                    {
                        pending.Push((next, nextCode, length + 1));
                    }
                }
            }

            return codes;
        }
    }
}
