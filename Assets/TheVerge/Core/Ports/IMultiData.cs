using TheVerge.Core.Multis;

namespace TheVerge.Core.Ports
{
    /// <summary>
    /// Дома и корабли из файлов данных (PLAN Стадия 2, п. 6). Реализация — Data/Multis/MultiTable; Core не знает,
    /// MultiCollection.uop это или multi.mul. Номер — по маске 0x3FFF, как у сервера (MultiData.GetComponents).
    /// </summary>
    public interface IMultiData
    {
        /// <summary>Сколько номеров есть в файле.</summary>
        int Count { get; }

        /// <summary>Части и границы; нет такого номера — <see cref="MultiComponents.Empty"/>.</summary>
        MultiComponents Get(int multiId);
    }
}
