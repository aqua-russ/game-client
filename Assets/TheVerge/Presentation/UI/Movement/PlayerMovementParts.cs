using TheVerge.Core.Movement;

namespace TheVerge.Presentation.UI.Movement
{
    /// <summary>Откуда экран отладки берёт числа своего персонажа (ядро, PLAN Стадия 3); без ходьбы — пусто.</summary>
    public readonly struct PlayerMovementParts
    {
        public PlayerMovementParts(PlayerMotion motion, MovementReplies replies, StepPacer pacer, StepAckLatency latency,
            PlayerStepShow show)
        {
            Motion = motion;
            Replies = replies;
            Pacer = pacer;
            Latency = latency;
            Show = show;
        }

        /// <summary>Ходьба есть (файлы данных прочитаны).</summary>
        public bool IsAvailable => Motion != null;

        public PlayerMotion Motion { get; }

        public MovementReplies Replies { get; }

        public StepPacer Pacer { get; }

        public StepAckLatency Latency { get; }

        public PlayerStepShow Show { get; }
    }
}
