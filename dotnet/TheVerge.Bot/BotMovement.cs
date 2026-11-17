using System;
using TheVerge.Core.Movement;
using TheVerge.Core.Ports;
using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>
    /// Движение своего персонажа у бота (PLAN Стадия 3): клавиши (<see cref="MoveIntent"/>, раскладка «север сверху»), темп
    /// (<see cref="StepPacer"/> — его тикает GameClient), предсказание шага и ответы сервера. Есть только с файлами данных
    /// (--data): без карты шаг не предсказать.
    /// </summary>
    public sealed class BotMovement : IDisposable
    {
        public BotMovement(PlayerMotion motion, MovementReplies replies, IClock clock, MovementCheck? check = null)
        {
            Motion = motion;
            Replies = replies;
            Check = check;
            Intent = new MoveIntent(clock, CameraLayout.NorthUp);
            Pacer = new StepPacer(Intent, motion);
            Stats = new MovementStats(motion, replies, clock);
        }

        public PlayerMotion Motion { get; }

        public MovementReplies Replies { get; }

        public MovementCheck? Check { get; }

        public MoveIntent Intent { get; }

        public StepPacer Pacer { get; }

        public MovementStats Stats { get; }

        /// <summary>Сервер подтвердил вход (0x1B): персонаж на его клетке, счёт шагов с нуля.</summary>
        public void OnPlayerConfirmed(PlayerState player) => Motion.EnterWorld(player.Position, player.Direction);

        public void Dispose() => Stats.Dispose();
    }
}
