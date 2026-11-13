namespace TheVerge.Core.World
{
    /// <summary>
    /// Счётчики необычных пакетов мира (PLAN Стадия 2, п. 7): движение неизвестного моба (создана заглушка), удаление
    /// неизвестного объекта (ничего не сделано), предмет в неизвестном контейнере (контейнер-заглушка), отказы.
    /// </summary>
    public sealed class WorldMetrics
    {
        public long UnknownMobileMoves { get; internal set; }

        public long UnknownRemoves { get; internal set; }

        public long StubContainers { get; internal set; }

        public long Rejected { get; internal set; }

        public override string ToString() =>
            "заглушек мобов " + UnknownMobileMoves + ", удалений неизвестного " + UnknownRemoves +
            ", заглушек контейнеров " + StubContainers + ", отказов " + Rejected;
    }
}
