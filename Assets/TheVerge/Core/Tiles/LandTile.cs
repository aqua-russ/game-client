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
    }
}
