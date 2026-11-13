namespace TheVerge.Data.Files
{
    /// <summary>
    /// Как называются куски в файле UOP: расширение, сколько номеров перебирать и сколько цифр в номере. Карта —
    /// «.dat», 0x14000, 8 цифр (TileMatrix.cs); MultiCollection — «.bin», 0x10000, 6 цифр (MultiData.cs).
    /// </summary>
    public readonly struct UopNames
    {
        public static readonly UopNames Map = new UopNames(".dat", 0x14000, 8);
        public static readonly UopNames Multi = new UopNames(".bin", 0x10000, 6);

        public UopNames(string extension, int count, int digits)
        {
            Extension = extension;
            Count = count;
            Digits = digits;
        }

        public string Extension { get; }
        public int Count { get; }
        public int Digits { get; }
    }
}
