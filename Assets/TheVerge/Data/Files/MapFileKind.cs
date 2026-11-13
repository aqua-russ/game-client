namespace TheVerge.Data.Files
{
    /// <summary>Формат файла карты: map{N}.mul или map{N}LegacyMUL.uop (TileMatrix.cs — сначала MUL).</summary>
    public enum MapFileKind
    {
        Mul,
        Uop,
    }
}
