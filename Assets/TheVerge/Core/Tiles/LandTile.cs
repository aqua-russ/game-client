namespace TheVerge.Core.Tiles
{
    /// <summary>
    /// Клетка земли: номер плитки и высота. Номер — как у сервера знаковый (LandTile.m_ID — short, TileMatrix.cs):
    /// в настоящих картах номера меньше 0x4000, а свойства ищутся по маске (<see cref="Ports.ITileData.Land"/>).
    /// </summary>
    public readonly struct LandTile
    {
        public LandTile(int id, int z)
        {
            Id = id;
            Z = z;
        }

        public int Id { get; }
        public int Z { get; }

        /// <summary>«Пустая» земля, которая не служит опорой: LandTile.Ignored сервера (TileMatrix.cs) — номера 2, 0x1DB и 0x1AE–0x1B5.</summary>
        public bool IsIgnored => Id == 2 || Id == 0x1DB || (Id >= 0x1AE && Id <= 0x1B5);
    }
}
