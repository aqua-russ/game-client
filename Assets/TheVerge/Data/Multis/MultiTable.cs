using System.Collections.Generic;
using TheVerge.Core.Multis;
using TheVerge.Core.Ports;
using TheVerge.Data.Files;

namespace TheVerge.Data.Multis
{
    /// <summary>
    /// Все дома и корабли в памяти, как у сервера (MultiData: читает всё при запуске; файлы небольшие). Номер — по маске
    /// 0x3FFF (GetComponents); нет номера — <see cref="MultiComponents.Empty"/>.
    /// </summary>
    public sealed class MultiTable : IMultiData
    {
        private const int IdMask = 0x3FFF;
        private readonly Dictionary<int, MultiComponents> _multis;

        public MultiTable(Dictionary<int, MultiComponents> multis)
        {
            _multis = multis;
        }

        public int Count => _multis.Count;

        public static MultiTable? Load(MultiFiles files, out string error)
        {
            var multis = files.Kind == MultiFileKind.Uop ? MultiUopReader.Read(files.UopPath, out error)
                : MultiMulReader.Read(files.IndexPath, files.DataPath, files.MulRecordSize, out error);
            return multis == null ? null : new MultiTable(multis);
        }

        public MultiComponents Get(int multiId) =>
            _multis.TryGetValue(multiId & IdMask, out var multi) ? multi : MultiComponents.Empty;
    }
}
