using TheVerge.Core.Primitives;

namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Шаги показа одного моба: текущий шаг картинки и очередь пришедших, но ещё не показанных (кольцо без новой памяти).
    /// <see cref="Target"/> — последняя клетка (от сервера или, у своего персонажа, предсказанная). Правила — чужие
    /// <see cref="OthersMotion"/>, свой <see cref="PlayerStepShow"/>; время шага — <see cref="StepTiming"/>.
    /// </summary>
    internal sealed class ShownSteps
    {
        // Отставание ограничено ClientLimits.StepBacklogSnapMs (1000 мс), самый короткий шаг — 100 мс: ждущих не больше 11.
        private const int Capacity = 16;

        private readonly Waiting[] _waiting = new Waiting[Capacity];
        private int _head;

        public Serial Serial { get; private set; }

        public Point3D Target { get; private set; }

        public ShownStep Current { get; private set; }

        public int WaitingCount { get; private set; }

        /// <summary>Встать на клетку сразу, без езды; ждущие шаги выбрасываются. Возвращает, сколько выброшено.</summary>
        public int SnapTo(Serial serial, Point3D cell, Direction facing, long now)
        {
            int dropped = WaitingCount;
            Serial = serial;
            Target = cell;
            Current = new ShownStep(new StepMove(cell, cell, facing, false), now, now, 0, 0);
            _head = 0;
            WaitingCount = 0;
            return dropped;
        }

        /// <summary>Шаг на <paramref name="to"/> пришёл: в очередь. Нет места — false (тогда перескок).</summary>
        public bool Enqueue(Point3D to, Direction facing, long arrivedMs, bool mounted)
        {
            if (WaitingCount == Capacity)
            {
                return false;
            }

            _waiting[(_head + WaitingCount) % Capacity] = new Waiting(new StepMove(Target, to, facing, mounted), arrivedMs);
            WaitingCount++;
            Target = to;
            return true;
        }

        /// <summary>Насколько картинка отстала от <see cref="Target"/>: остаток текущего шага и ждущие по обычному времени.</summary>
        public int BacklogMs(long now)
        {
            long backlog = Current.EndMs > now ? Current.EndMs - now : 0;
            for (int i = 0; i < WaitingCount; i++)
            {
                var step = _waiting[(_head + i) % Capacity];
                backlog += StepTiming.StepDelayMs(step.Move.IsMounted, step.Move.IsRunning);
            }

            return (int)backlog;
        }

        /// <summary>
        /// Текущий шаг кончился к <paramref name="now"/> и есть ждущий — начать его: сразу после конца текущего (или с
        /// прихода, если пришёл позже), отставание — число ждущих вместе с ним (догонялка — <see cref="StepTiming.DisplayMs"/>).
        /// </summary>
        public bool TryStartNext(long now, out ShownStep started)
        {
            if (WaitingCount == 0 || Current.EndMs > now)
            {
                started = default;
                return false;
            }

            var next = _waiting[_head];
            int behind = WaitingCount;
            _head = (_head + 1) % Capacity;
            WaitingCount--;
            long start = next.ArrivedMs > Current.EndMs ? next.ArrivedMs : Current.EndMs;
            int duration = StepTiming.DisplayMs(next.Move.IsMounted, next.Move.IsRunning, behind);
            started = new ShownStep(next.Move, next.ArrivedMs, start, duration, behind);
            Current = started;
            return true;
        }

        private readonly struct Waiting
        {
            public Waiting(StepMove move, long arrivedMs)
            {
                Move = move;
                ArrivedMs = arrivedMs;
            }

            public StepMove Move { get; }

            public long ArrivedMs { get; }
        }
    }
}
