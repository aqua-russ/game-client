namespace TheVerge.Data.Files
{
    /// <summary>
    /// Хеш имён кусков UOP: hashlittle2 из lookup3 Боба Дженкинса (общественное достояние, burtleburtle.net/bob/c/lookup3.c),
    /// как его считает сервер (UOPFiles.HashLittle2): по символам строки (имена — ASCII), результат — (b &lt;&lt; 32) | c.
    /// </summary>
    public static class UopHash
    {
        public static ulong HashLittle2(string text)
        {
            var length = text.Length;
            uint a, b, c;
            a = b = c = 0xDEADBEEF + (uint)length;
            var k = 0;
            while (length > 12)
            {
                a += Word(text, k);
                b += Word(text, k + 4);
                c += Word(text, k + 8);
                Mix(ref a, ref b, ref c);
                length -= 12;
                k += 12;
            }

            if (length == 0)
            {
                return ((ulong)b << 32) | c;
            }

            a += Tail(text, k, length, 0);
            b += Tail(text, k, length, 4);
            c += Tail(text, k, length, 8);
            Final(ref a, ref b, ref c);
            return ((ulong)b << 32) | c;
        }

        private static uint Word(string text, int at) =>
            text[at] + ((uint)text[at + 1] << 8) + ((uint)text[at + 2] << 16) + ((uint)text[at + 3] << 24);

        // Остаток короче 12 символов: недостающие считаются нулями (ветки switch из lookup3).
        private static uint Tail(string text, int start, int length, int from)
        {
            uint word = 0;
            for (var i = 0; i < 4 && from + i < length; i++)
            {
                word += (uint)text[start + from + i] << (8 * i);
            }

            return word;
        }

        private static uint Rot(uint x, int k) => (x << k) | (x >> (32 - k));

        private static void Mix(ref uint a, ref uint b, ref uint c)
        {
            a -= c; a ^= Rot(c, 4); c += b;
            b -= a; b ^= Rot(a, 6); a += c;
            c -= b; c ^= Rot(b, 8); b += a;
            a -= c; a ^= Rot(c, 16); c += b;
            b -= a; b ^= Rot(a, 19); a += c;
            c -= b; c ^= Rot(b, 4); b += a;
        }

        private static void Final(ref uint a, ref uint b, ref uint c)
        {
            c ^= b; c -= Rot(b, 14);
            a ^= c; a -= Rot(c, 11);
            b ^= a; b -= Rot(a, 25);
            c ^= b; c -= Rot(b, 16);
            a ^= c; a -= Rot(c, 4);
            b ^= a; b -= Rot(a, 14);
            c ^= b; c -= Rot(b, 24);
        }
    }
}
