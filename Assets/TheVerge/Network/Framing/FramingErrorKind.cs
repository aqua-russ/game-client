namespace TheVerge.Network.Framing
{
    /// <summary>Почему поток нельзя резать дальше. Каждая причина ведёт к дисконнекту с дампом (AGENTS.md §3.5).</summary>
    public enum FramingErrorKind : byte
    {
        None = 0,

        /// <summary>Номера нет в матрице пакетов для этого направления: длина неизвестна.</summary>
        UnknownPacket = 1,

        /// <summary>Подкоманды нет в матрице (docs/protocol/packets.md: пакеты, которых нет в сервере).</summary>
        UnknownSubCommand = 2,

        /// <summary>Объявленная длина меньше заголовка пакета.</summary>
        BadLength = 3,
    }
}
