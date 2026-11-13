namespace TheVerge.Data.Files
{
    /// <summary>Кусок файла UOP: где начинаются данные (после заголовка куска), полная и сжатая длина (UOPEntry сервера).</summary>
    public readonly struct UopEntry
    {
        public UopEntry(long offset, int size, int compressedSize, bool compressed)
        {
            Offset = offset;
            Size = size;
            CompressedSize = compressedSize;
            Compressed = compressed;
        }

        public long Offset { get; }
        public int Size { get; }
        public int CompressedSize { get; }
        public bool Compressed { get; }
    }
}
