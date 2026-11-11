namespace TheVerge.Core.Session
{
    /// <summary>
    /// Лимиты сервера ModernUO на нашем стенде. Единственное место этих чисел в коде клиента (AGENTS.md §3.5).
    /// Источник и ключи настроек сервера — docs/TARGET.md §3 (сверено с Movement.cs, MovementThrottle.cs,
    /// ExpansionConfiguration.cs и throttles.json). Изменился сервер — меняются TARGET.md и этот класс.
    /// Детектор спидхака (кредит на джиттер, пороги темпа) — внутреннее дело сервера, сюда не входит.
    /// </summary>
    public static class ServerLimits
    {
        // Интервал между шагами, мс (movement.delay.*). Поворот на месте задержки не имеет.
        public const int WalkFootDelayMs = 400;
        public const int RunFootDelayMs = 200;
        public const int WalkMountDelayMs = 200;
        public const int RunMountDelayMs = 100;
        public const int TurnDelayMs = 0;

        // Sequence шага: первый шаг 0, далее 1–255, после 255 снова 1; отказ сервера сбрасывает в 0.
        public const byte FirstMoveSequence = 0;
        public const byte WrappedMoveSequence = 1;
        public const byte MaxMoveSequence = 255;

        /// <summary>Шагов без подтверждения, после которых клиент ждёт ответа (настройка клиента).</summary>
        public const int MaxUnacknowledgedSteps = 3;

        /// <summary>Сервер сбрасывает очередь шагов при таком размере (movementThrottle.hardQueueLimit).</summary>
        public const int ServerStepQueueLimit = 10;

        /// <summary>Речь 0x03 и 0xAD, текстовые команды 0x12: не чаще, мс (throttles.json).</summary>
        public const int SpeechThrottleMs = 25;

        /// <summary>Переименование 0x75: не чаще, мс (throttles.json).</summary>
        public const int RenameThrottleMs = 500;

        /// <summary>Задержка между действиями, мс (actionDelay, значение pre-AoS).</summary>
        public const int ActionDelayMs = 500;

        /// <summary>Максимум одного стата (stats.statMax).</summary>
        public const int StatMax = 100;
    }
}
