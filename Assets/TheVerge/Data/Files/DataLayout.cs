using System.Collections.Generic;

namespace TheVerge.Data.Files
{
    /// <summary>Что найдено в папке файлов данных и в каком формате (PLAN Стадия 2, п. 1). Пишется в лог при старте.</summary>
    public sealed class DataLayout
    {
        public DataLayout(string folder, MapFiles map, TileDataFormat tileData, MultiFiles multi)
        {
            Folder = folder;
            Map = map;
            TileData = tileData;
            Multi = multi;
        }

        public string Folder { get; }
        public MapFiles Map { get; }
        public TileDataFormat TileData { get; }
        public MultiFiles Multi { get; }

        /// <summary>Строки для лога: папка, карта, tiledata, дома.</summary>
        public IReadOnlyList<string> Describe()
        {
            var definition = Map.Definition;
            return new[]
            {
                "Папка данных: " + Folder,
                "Карта " + definition.Name + " (" + definition.Index + ", " + definition.Width + "×" + definition.Height + "): " +
                FileName(Map.MapPath) + " (" + (Map.Kind == MapFileKind.Uop ? "UOP" : "MUL") + "), статика: " +
                FileName(Map.IndexPath) + " + " + FileName(Map.StaticsPath),
                "Tiledata: " + FileName(TileData.Path) + " — флаги " + (TileData.Is64BitFlags ? 64 : 32) + " бита, предметов 0x" +
                TileData.ItemCount.ToString("X"),
                "Дома и корабли: " + DescribeMulti(),
            };
        }

        private string DescribeMulti() => Multi.Kind == MultiFileKind.Uop
            ? FileName(Multi.UopPath) + " (UOP)"
            : FileName(Multi.IndexPath) + " + " + FileName(Multi.DataPath) + " (MUL, " + Multi.MulRecordSize + " байт на компонент)";

        private static string FileName(string path) => System.IO.Path.GetFileName(path);
    }
}
