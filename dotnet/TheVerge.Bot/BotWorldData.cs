using TheVerge.Core.Ports;
using TheVerge.Data.Files;

namespace TheVerge.Bot
{
    /// <summary>Файлы данных, которые бот прочитал по --data: что найдено и прочитанные таблицы.</summary>
    public sealed class BotWorldData
    {
        public BotWorldData(DataLayout layout, ITileData tiles)
        {
            Layout = layout;
            Tiles = tiles;
        }

        public DataLayout Layout { get; }
        public ITileData Tiles { get; }
    }
}
