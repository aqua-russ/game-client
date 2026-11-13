using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>Что бот видит в мире — одной строкой для лога: сколько объектов, свой персонаж и необычные пакеты.</summary>
    internal static class BotWorldSummary
    {
        public static string Describe(EntityStore entities)
        {
            var text = "мобов " + entities.MobileCount + ", предметов " + entities.ItemCount;
            var player = entities.Player;
            if (player != null)
            {
                text += "; свой персонаж — тело 0x" + player.Body.ToString("X4") + " в " + player.Position + ", надето " + player.EquippedCount;
            }

            var m = entities.Metrics;
            if (m.UnknownMobileMoves + m.UnknownRemoves + m.StubContainers + m.Rejected > 0)
            {
                text += "; необычное: " + m;
            }

            return text;
        }
    }
}
