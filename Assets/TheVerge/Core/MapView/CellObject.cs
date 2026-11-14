using TheVerge.Core.Tiles;

namespace TheVerge.Core.MapView
{
    /// <summary>
    /// Один объект на клетке для окна клетки: что это (<see cref="Kind"/>), номер плитки (у моба — тело), Z и данные из
    /// tiledata. <see cref="Height"/> — по tiledata (<see cref="ItemTileData.CalcHeight"/>), у земли и мобов её нет.
    /// <see cref="Serial"/> — у предметов и мобов; у части дома — серийник дома; у земли и статики 0.
    /// </summary>
    public readonly struct CellObject
    {
        public CellObject(SceneKind kind, int id, int z, int? height, TileFlags flags, string name, CellEntity entity)
        {
            Kind = kind;
            Id = id;
            Z = z;
            Height = height;
            Flags = flags;
            Name = name;
            Entity = entity;
        }

        public SceneKind Kind { get; }
        public int Id { get; }
        public int Z { get; }
        public int? Height { get; }
        public TileFlags Flags { get; }
        public string Name { get; }
        public CellEntity Entity { get; }
        public uint Serial => Entity.Serial;
        public int Hue => Entity.Hue;
        public int Amount => Entity.Amount;
    }
}
