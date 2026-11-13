using TheVerge.Data.Map;

namespace TheVerge.Data.Files
{
    /// <summary>Файлы одной карты: земля (MUL или UOP) и статика (staidx{N}.mul — индекс, statics{N}.mul — данные).</summary>
    public sealed class MapFiles
    {
        public MapFiles(MapDefinition definition, MapFileKind kind, string mapPath, string indexPath, string staticsPath)
        {
            Definition = definition;
            Kind = kind;
            MapPath = mapPath;
            IndexPath = indexPath;
            StaticsPath = staticsPath;
        }

        public MapDefinition Definition { get; }
        public MapFileKind Kind { get; }
        public string MapPath { get; }
        public string IndexPath { get; }
        public string StaticsPath { get; }
    }
}
