using TheVerge.Core.Diagnostics;

namespace TheVerge.Bootstrap
{
    /// <summary>С чем собирается клиент одного входа в Unity (<see cref="UnityLoginClient"/>): общее на весь запуск игры.</summary>
    public readonly struct UnityClientOptions
    {
        /// <param name="data">Файлы данных; null — мир без ходьбы (шаг не предсказать без карты).</param>
        /// <param name="lag">Настройки симуляции пинга (PLAN Стадия 3, п. 9).</param>
        public UnityClientOptions(UnityWorldData data, LagSimulation lag)
        {
            Data = data;
            Lag = lag;
        }

        public UnityWorldData Data { get; }

        public LagSimulation Lag { get; }
    }
}
