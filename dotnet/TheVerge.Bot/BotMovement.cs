using TheVerge.Core.Movement;
using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>
    /// Движение своего персонажа у бота (PLAN Стадия 3): предсказание шага и ответы сервера. Есть только с файлами данных
    /// (--data): без карты шаг не предсказать.
    /// </summary>
    public sealed class BotMovement
    {
        public BotMovement(PlayerMotion motion, MovementReplies replies)
        {
            Motion = motion;
            Replies = replies;
        }

        public PlayerMotion Motion { get; }

        public MovementReplies Replies { get; }

        /// <summary>Сервер подтвердил вход (0x1B): персонаж на его клетке, счёт шагов с нуля.</summary>
        public void OnPlayerConfirmed(PlayerState player) => Motion.EnterWorld(player.Position, player.Direction);
    }
}
