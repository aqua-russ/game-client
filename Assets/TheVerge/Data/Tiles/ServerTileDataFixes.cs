using TheVerge.Core.Tiles;

namespace TheVerge.Data.Tiles
{
    /// <summary>
    /// Правки tiledata, которые сервер делает при запуске поверх файла (методы Initialize в UOContent): по исправленной
    /// таблице он решает, видно ли сквозь предмет и какой он высоты, поэтому клиент повторяет их. Найдены сверкой
    /// tools/check/map.sh; при обновлении ModernUO — искать в UOContent запись в «TileData.ItemTable[».
    /// </summary>
    public static class ServerTileDataFixes
    {
        public static void Apply(TileDataTable table)
        {
            // Special Systems/Engines/ItemFixes.cs: «Missing NoShoot flags», «Incorrect height».
            AddFlags(table, 0x2A0, TileFlags.NoShoot);
            AddFlags(table, 0x3E0, TileFlags.NoShoot);
            AddFlags(table, 0x3E1, TileFlags.NoShoot);
            SetHeight(table, 0x34D2, 0);

            // Engines/ConPVP/Games/CTF.cs, Initialize.
            for (var id = 0x7C9; id <= 0x7D0; id++)
            {
                AddFlags(table, id, TileFlags.NoShoot);
            }

            // Items/Misc/LOSBlocker.cs: флаги заменяются целиком.
            SetFlags(table, 0x21A2, TileFlags.Wall | TileFlags.NoShoot);
            SetHeight(table, 0x21A2, 20);

            // Items/Skill Items/Magical/Misc/PotionKeg.cs.
            SetHeight(table, 0x1940, 4);
        }

        private static void AddFlags(TileDataTable table, int id, TileFlags flags) => SetFlags(table, id, table.Item(id).Flags | flags);

        private static void SetFlags(TileDataTable table, int id, TileFlags flags)
        {
            var item = table.Item(id);
            table.SetItem(id, new ItemTileData(item.Name, flags, Properties(item, item.Height)));
        }

        private static void SetHeight(TileDataTable table, int id, int height)
        {
            var item = table.Item(id);
            table.SetItem(id, new ItemTileData(item.Name, item.Flags, Properties(item, height)));
        }

        private static ItemTileProperties Properties(ItemTileData item, int height) =>
            new ItemTileProperties((byte)item.Weight, (byte)item.Quality, (ushort)item.Animation, (byte)item.Quantity,
                (byte)item.Value, (byte)height);
    }
}
