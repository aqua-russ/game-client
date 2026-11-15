using System;
using TheVerge.Core.Movement;
using TheVerge.Core.Ports;
using TheVerge.Core.World;
using TheVerge.Network.Packets.Movement;
using TheVerge.Network.Session;

namespace TheVerge.Bootstrap
{
    /// <summary>
    /// Движение своего персонажа в Unity (PLAN Стадия 3, п. 9) — как BotMovement у бота (dotnet/TheVerge.Bot): клавиши
    /// (<see cref="MoveIntent"/>), темп (<see cref="StepPacer"/>), предсказание шага (<see cref="PlayerMotion"/>) и ответы
    /// сервера (<see cref="MovementReplies"/>); сверх бота — показ шагов (<see cref="PlayerStepShow"/>). Есть только с
    /// файлами данных: без карты шаг не предсказать.
    /// </summary>
    public sealed class UnityMovement : IDisposable
    {
        private UnityMovement(PlayerMotion motion, MovementReplies replies, IClock clock)
        {
            Motion = motion;
            Replies = replies;
            Intent = new MoveIntent(clock, CameraLayout.NorthUp);
            Pacer = new StepPacer(Intent, motion);
            Show = new PlayerStepShow(motion, clock);
        }

        public PlayerMotion Motion { get; }

        public MovementReplies Replies { get; }

        public MoveIntent Intent { get; }

        public StepPacer Pacer { get; }

        public PlayerStepShow Show { get; }

        /// <summary>Обработчики шагов (0x22, 0x21) — в диспетчер; 0x20 и смена карты идут через мир (WorldHandlers).</summary>
        public static UnityMovement Create(PacketDispatcher dispatcher, NetworkSession session, EntityStore entities,
            UnityWorldData data, IClock clock, ILog log)
        {
            var sender = new MovementSender(session);
            var motion = new PlayerMotion(entities, new MovementCheck(data.Map, data.Tiles, data.Multis, entities), sender);
            var replies = new MovementReplies(motion, sender, log);
            MovementHandlers.Register(dispatcher, replies);
            return new UnityMovement(motion, replies, clock);
        }

        /// <summary>Сервер подтвердил вход (0x1B): персонаж на его клетке, счёт шагов с нуля.</summary>
        public void OnPlayerConfirmed(PlayerState player) => Motion.EnterWorld(player.Position, player.Direction);

        public void Dispose() => Show.Dispose();
    }
}
