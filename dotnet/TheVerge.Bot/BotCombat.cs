using TheVerge.Core.Combat;

namespace TheVerge.Bot
{
    /// <summary>
    /// Бой у бота (PLAN Стадия 4, п. 7–8): стойка и противник по пакетам сервера, запросы стойки и атаки, журнал боя.
    /// </summary>
    public sealed class BotCombat
    {
        public BotCombat(CombatState state, CombatCommands commands, CombatLog log)
        {
            State = state;
            Commands = commands;
            Log = log;
        }

        public CombatState State { get; }

        public CombatCommands Commands { get; }

        /// <summary>Журнал боя: замахи, анимации, урон, эффекты, звуки — в порядке пакетов сервера.</summary>
        public CombatLog Log { get; }
    }
}
