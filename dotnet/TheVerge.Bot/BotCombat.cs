using TheVerge.Core.Combat;

namespace TheVerge.Bot
{
    /// <summary>Бой у бота (PLAN Стадия 4, п. 7): стойка и противник по пакетам сервера, запросы стойки и атаки.</summary>
    public sealed class BotCombat
    {
        public BotCombat(CombatState state, CombatCommands commands)
        {
            State = state;
            Commands = commands;
        }

        public CombatState State { get; }

        public CombatCommands Commands { get; }
    }
}
