using TheVerge.Core.Status;

namespace TheVerge.Bot
{
    /// <summary>Состояние мобов у бота (PLAN Стадия 4, п. 4): обновления по пакетам и запросы статуса.</summary>
    public sealed class BotStatus
    {
        public BotStatus(MobileStatusUpdates updates, StatusCommands commands)
        {
            Updates = updates;
            Commands = commands;
        }

        public MobileStatusUpdates Updates { get; }
        public StatusCommands Commands { get; }
    }
}
