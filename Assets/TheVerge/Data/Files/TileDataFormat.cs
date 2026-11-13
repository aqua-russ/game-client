namespace TheVerge.Data.Files
{
    /// <summary>
    /// Раскладка tiledata.mul. Сервер выбирает её по длине файла (TileData.cs, Load): от 3 188 736 байт (клиенты
    /// 7.0.9.0+) — флаги 64 бита и 0x10000 предметов, от 1 644 544 (7.0.0.0) — 32 бита и 0x8000, меньше — 32 бита и
    /// 0x4000. Земли всегда 0x4000. Перед каждой группой из 32 записей — 4 байта заголовка.
    /// </summary>
    public readonly struct TileDataFormat
    {
        public const int LandCount = 0x4000;
        public const int GroupSize = 32;
        public const int GroupHeaderSize = 4;
        public const int NameSize = 20;
        public const long Length64Bit = 3188736;
        public const long Length32Bit = 1644544;

        public TileDataFormat(string path, bool is64BitFlags, int itemCount)
        {
            Path = path;
            Is64BitFlags = is64BitFlags;
            ItemCount = itemCount;
        }

        public string Path { get; }
        public bool Is64BitFlags { get; }
        public int ItemCount { get; }

        /// <summary>Флаги (4 или 8) + номер текстуры (2) + имя (20).</summary>
        public int LandRecordSize => FlagsSize + 2 + NameSize;

        /// <summary>Флаги + вес, качество (слой), анимация (2), байт, количество, 4 байта, байт, значение, высота + имя.</summary>
        public int ItemRecordSize => FlagsSize + 13 + NameSize;

        public long LandSectionLength => Section(LandCount, LandRecordSize);

        /// <summary>Сколько байт сервер прочтёт из файла: меньше — файл не подходит (сервер упал бы при запуске).</summary>
        public long RequiredLength => LandSectionLength + Section(ItemCount, ItemRecordSize);

        private int FlagsSize => Is64BitFlags ? 8 : 4;

        public static TileDataFormat ForLength(string path, long length)
        {
            if (length >= Length64Bit)
            {
                return new TileDataFormat(path, true, 0x10000);
            }

            return new TileDataFormat(path, false, length >= Length32Bit ? 0x8000 : 0x4000);
        }

        private static long Section(int count, int recordSize) => (long)(count / GroupSize) * (GroupHeaderSize + GroupSize * recordSize);
    }
}
