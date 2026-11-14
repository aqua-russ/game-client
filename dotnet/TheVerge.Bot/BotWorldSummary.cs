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

        /// <summary>Статус своего персонажа одной строкой; пусто — сервер полного статуса ещё не прислал.</summary>
        public static string DescribeStatus(EntityStore entities)
        {
            var status = entities.Player?.Status;
            if (status == null || status.Version <= 0)
            {
                return string.Empty;
            }

            var a = status.Attributes;
            return "«" + status.Name + "», здоровье " + status.Hits + ", мана " + status.Mana + ", выносливость " + status.Stam +
                   ", сила " + a.Str + ", ловкость " + a.Dex + ", интеллект " + a.Int + ", золото " + a.Gold + ", броня " + a.Armor +
                   ", вес " + a.Weight;
        }
    }
}
