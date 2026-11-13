using System.IO;
using TheVerge.Data.Map;

namespace TheVerge.Data.Files
{
    /// <summary>
    /// Находит файлы данных и определяет их формат так же, как сервер (PLAN Стадия 2, п. 1): карта — map{N}.mul, а
    /// если его нет, map{N}LegacyMUL.uop (TileMatrix.cs); tiledata — по длине (TileData.cs); дома — MultiCollection.uop,
    /// а если его нет, multi.idx + multi.mul (MultiData.cs). Нет файла или длина не подходит — ошибка с именем файла.
    /// Сервер без карты и статики работает с пустой землёй, а клиенту без них показывать нечего, поэтому здесь это ошибка.
    /// </summary>
    public static class DataLayoutDetector
    {
        private const int LandBlockSize = 196;
        private const int StaticIndexRecordSize = 12;

        public static DataDetection Detect(string folder, MapDefinition map, DataOptions options)
        {
            if (!Directory.Exists(folder))
            {
                return DataDetection.Failed("Папка файлов данных не найдена: «" + folder + "».");
            }

            var files = new DataFolder(folder);
            var error = DetectMap(files, map, out var mapFiles);
            if (error.Length == 0)
            {
                error = DetectTileData(files, out var tileData);
                if (error.Length == 0)
                {
                    error = DetectMulti(files, options, out var multi);
                    if (error.Length == 0)
                    {
                        return DataDetection.Found(new DataLayout(folder, mapFiles!, tileData, multi!));
                    }
                }
            }

            return DataDetection.Failed(error);
        }

        private static string DetectMap(DataFolder files, MapDefinition map, out MapFiles? result)
        {
            result = null;
            var mulName = "map" + map.FileIndex + ".mul";
            var uopName = "map" + map.FileIndex + "LegacyMUL.uop";
            var mapPath = files.Find(mulName);
            var kind = MapFileKind.Mul;
            var error = mapPath == null ? string.Empty : CheckLength(mapPath, map, map.BlockCount * LandBlockSize);
            if (mapPath == null)
            {
                mapPath = files.Find(uopName);
                kind = MapFileKind.Uop;
                error = mapPath == null
                    ? "В папке «" + files.Path + "» нет файла карты: ни " + mulName + ", ни " + uopName + "."
                    : UopHeader.Check(mapPath, UopHeader.MapVersion);
            }

            var indexName = "staidx" + map.FileIndex + ".mul";
            var indexPath = error.Length == 0 ? Require(files, indexName, "индекс статики", ref error) : null;
            if (indexPath != null)
            {
                error = CheckLength(indexPath, map, map.BlockCount * StaticIndexRecordSize);
            }

            var staticsPath = error.Length == 0 ? Require(files, "statics" + map.FileIndex + ".mul", "статика", ref error) : null;
            if (error.Length == 0)
            {
                result = new MapFiles(map, kind, mapPath!, indexPath!, staticsPath!);
            }

            return error;
        }

        private static string DetectTileData(DataFolder files, out TileDataFormat format)
        {
            format = default;
            var error = string.Empty;
            var path = Require(files, "tiledata.mul", "свойства плиток", ref error);
            if (path == null)
            {
                return error;
            }

            var length = DataFolder.Length(path);
            format = TileDataFormat.ForLength(path, length);
            return length >= format.RequiredLength
                ? string.Empty
                : "tiledata.mul короче, чем нужно: " + length + " байт, нужно не меньше " + format.RequiredLength + ".";
        }

        private static string DetectMulti(DataFolder files, DataOptions options, out MultiFiles? multi)
        {
            multi = null;
            var uopPath = files.Find("MultiCollection.uop");
            if (uopPath != null)
            {
                var error = UopHeader.Check(uopPath, UopHeader.MultiVersion);
                multi = error.Length == 0 ? MultiFiles.Uop(uopPath) : null;
                return error;
            }

            var indexPath = files.Find("multi.idx");
            var dataPath = files.Find("multi.mul");
            if (indexPath == null || dataPath == null)
            {
                return "В папке «" + files.Path + "» нет файлов домов и кораблей: ни MultiCollection.uop, ни пары multi.idx и " +
                       "multi.mul (нет " + (indexPath == null ? "multi.idx" : "multi.mul") + ").";
            }

            multi = MultiFiles.Mul(indexPath, dataPath, options.MultiMulPostHsFormat);
            return string.Empty;
        }

        private static string? Require(DataFolder files, string name, string what, ref string error)
        {
            var path = files.Find(name);
            if (path == null)
            {
                error = "В папке «" + files.Path + "» нет файла " + name + " (" + what + ").";
            }

            return path;
        }

        private static string CheckLength(string path, MapDefinition map, long required)
        {
            var length = DataFolder.Length(path);
            return length >= required
                ? string.Empty
                : Path.GetFileName(path) + " короче, чем нужно для карты " + map.Name + " " + map.Width + "×" + map.Height + ": " +
                  length + " байт, нужно не меньше " + required + ".";
        }
    }
}
