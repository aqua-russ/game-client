using TheVerge.Core.Primitives;

namespace TheVerge.Core.Session
{
    /// <summary>
    /// Стартовый город из списка игрового сервера (для создания персонажа, TARGET.md §4): номер, название, здание,
    /// место появления и номер текста описания (cliloc).
    /// </summary>
    public readonly struct StartingCity
    {
        public StartingCity(int index, string city, string building, Point3D location, int mapId, int description)
        {
            Index = index;
            City = city;
            Building = building;
            Location = location;
            MapId = mapId;
            Description = description;
        }

        public int Index { get; }

        public string City { get; }

        public string Building { get; }

        /// <summary>Где появится персонаж.</summary>
        public Point3D Location { get; }

        public int MapId { get; }

        /// <summary>Номер текста описания в cliloc (файлы данных UO).</summary>
        public int Description { get; }

        public override string ToString() => City + ", " + Building;
    }
}
