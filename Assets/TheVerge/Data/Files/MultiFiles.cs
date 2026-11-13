namespace TheVerge.Data.Files
{
    /// <summary>Файлы домов и кораблей: MultiCollection.uop или пара multi.idx + multi.mul с размером записи.</summary>
    public sealed class MultiFiles
    {
        private MultiFiles(MultiFileKind kind, string uopPath, string indexPath, string dataPath, int mulRecordSize)
        {
            Kind = kind;
            UopPath = uopPath;
            IndexPath = indexPath;
            DataPath = dataPath;
            MulRecordSize = mulRecordSize;
        }

        public MultiFileKind Kind { get; }

        /// <summary>MultiCollection.uop; пусто для MUL.</summary>
        public string UopPath { get; }

        /// <summary>multi.idx; пусто для UOP.</summary>
        public string IndexPath { get; }

        /// <summary>multi.mul; пусто для UOP.</summary>
        public string DataPath { get; }

        /// <summary>Байт на компонент в multi.mul: 16 или 12 (<see cref="DataOptions.MultiMulPostHsFormat"/>); для UOP — 0.</summary>
        public int MulRecordSize { get; }

        public static MultiFiles Uop(string path) => new MultiFiles(MultiFileKind.Uop, path, string.Empty, string.Empty, 0);

        public static MultiFiles Mul(string indexPath, string dataPath, bool postHsFormat) =>
            new MultiFiles(MultiFileKind.Mul, string.Empty, indexPath, dataPath, postHsFormat ? 16 : 12);
    }
}
