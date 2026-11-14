using TheVerge.Core.Tiles;

namespace TheVerge.Core.Multis
{
    /// <summary>Часть дома или корабля: номер плитки предмета, сдвиг от центра и флаги (MultiTileEntry сервера, MultiData.cs).</summary>
    public readonly struct MultiComponent
    {
        public MultiComponent(int itemId, int x, int y, int z, TileFlags flags)
        {
            ItemId = itemId;
            X = x;
            Y = y;
            Z = z;
            Flags = flags;
        }

        public int ItemId { get; }
        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        /// <summary>0 — часть видна только как часть дизайна (в границы не входит, кроме первой); как у сервера.</summary>
        public TileFlags Flags { get; }

        /// <summary>Высота части в мире: сервер хранит её в sbyte (StaticTile.Z, TileMatrix.cs), поэтому сумма переполняется так же.</summary>
        public int WorldZ(int multiZ) => (sbyte)((sbyte)Z + multiZ);
    }
}
