using TheVerge.Core.Primitives;

namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Шаг показа чужого моба (PLAN Стадия 3, п. 8; ADR 0020 п. 4): картинка едет из <see cref="From"/> в <see cref="To"/>
    /// с <see cref="StartMs"/> за <see cref="DurationMs"/>. Моб стоит — <see cref="From"/> = <see cref="To"/>, длительность 0.
    /// Unity (задача 3.9) только читает и сглаживает по <see cref="Progress"/>.
    /// </summary>
    public readonly struct ShownStep
    {
        public ShownStep(StepMove move, long arrivedMs, long startMs, int durationMs, int behind)
        {
            Move = move;
            ArrivedMs = arrivedMs;
            StartMs = startMs;
            DurationMs = durationMs;
            Behind = behind;
        }

        public StepMove Move { get; }

        public Point3D From => Move.From;

        public Point3D To => Move.To;

        /// <summary>Направление шага от сервера (с битом бега), куда смотреть во время шага.</summary>
        public Direction Facing => Move.Facing;

        public bool IsRunning => Move.IsRunning;

        public bool IsMounted => Move.IsMounted;

        /// <summary>Когда пришёл пакет шага: <see cref="StartMs"/> позже него, только если картинка ещё показывала прежний шаг.</summary>
        public long ArrivedMs { get; }

        public long StartMs { get; }

        public int DurationMs { get; }

        /// <summary>На сколько шагов картинка отставала в начале этого (считая его); больше 1 — догонялка.</summary>
        public int Behind { get; }

        public long EndMs => StartMs + DurationMs;

        public bool IsCatchUp => Behind > 1;

        /// <summary>Сколько шага пройдено к <paramref name="now"/>: 0 — в начале, 1 — в конце и после.</summary>
        public double Progress(long now)
        {
            if (now >= EndMs)
            {
                return 1;
            }

            return now <= StartMs ? 0 : (double)(now - StartMs) / DurationMs;
        }
    }
}
