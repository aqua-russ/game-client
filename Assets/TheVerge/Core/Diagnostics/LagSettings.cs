using System;

namespace TheVerge.Core.Diagnostics
{
    /// <summary>
    /// Задержка пакетов одного направления в режиме «симуляция пинга» (PLAN Стадия 3, п. 9; ADR 0020 п. 9): постоянная часть
    /// и разброс ±. <see cref="None"/> — без задержки.
    /// </summary>
    public readonly struct LagSettings
    {
        public static readonly LagSettings None = default;

        public LagSettings(int delayMs, int jitterMs)
        {
            DelayMs = delayMs;
            JitterMs = jitterMs;
        }

        public int DelayMs { get; }

        public int JitterMs { get; }

        public bool IsNone => DelayMs == 0 && JitterMs == 0;

        /// <summary>Задержка одного пакета, мс: постоянная часть плюс равномерно от −разброса до +разброса, не меньше нуля.</summary>
        public int SampleMs(Random random) =>
            JitterMs == 0 ? DelayMs : Math.Max(0, DelayMs + random.Next(-JitterMs, JitterMs + 1));
    }
}
