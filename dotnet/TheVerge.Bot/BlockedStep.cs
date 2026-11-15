using TheVerge.Core.Movement;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;

namespace TheVerge.Bot
{
    /// <summary>
    /// blocked:&lt;направление&gt; (PLAN Стадия 3, п. 10; tools/check/scenes.sh, закрытая дверь): держать клавиши
    /// <see cref="HoldMs"/> — впереди преграда, клиент сам не пускает (его проверка шага), шаг к серверу не уходит и отказа
    /// нет. Поворот на месте уходит, как у игрока. Шаг ушёл — действие не удалось.
    /// </summary>
    internal sealed class BlockedStep : IBotStep
    {
        private const int HoldMs = 1500;

        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly IClock _clock;
        private readonly Direction _direction;
        private bool _started;
        private long _startedAt;
        private int _sentBefore;
        private int _rejectsBefore;

        public BlockedStep(BotWorld world, ILog log, IClock clock, string argument)
        {
            _world = world;
            _log = log;
            _clock = clock;
            CompassDirection.TryParse(argument, out _direction);
        }

        public string TimeoutProblem => "персонаж так и не встал в мире.";

        public string Failure { get; private set; } = string.Empty;

        public bool Poll()
        {
            var movement = _world.Movement;
            if (movement == null)
            {
                Failure = "нужны файлы данных (--data): без карты шаг не предсказать.";
                return false;
            }

            if (_world.Entities.Player == null || !movement.Motion.IsPlaced)
            {
                return false;
            }

            if (!_started)
            {
                Start(movement);
                return false;
            }

            if (movement.Motion.MovesSent != _sentBefore || movement.Replies.RejectCount != _rejectsBefore)
            {
                Failure = "шаг ушёл, хотя впереди ждали преграду: персонаж в " + movement.Motion.Position + ".";
                return false;
            }

            if (_clock.NowMs - _startedAt < HoldMs || movement.Motion.PendingSteps > 0)
            {
                return false;
            }

            _log.Info("Преграда " + CompassDirection.NameOf(_direction) + ": шаг не ушёл.");
            return true;
        }

        public void Finish()
        {
            if (_started)
            {
                _world.Movement?.Intent.Release(CompassDirection.KeysFor(_direction));
            }
        }

        private void Start(BotMovement movement)
        {
            movement.Intent.Press(CompassDirection.KeysFor(_direction));
            _sentBefore = movement.Motion.MovesSent;
            _rejectsBefore = movement.Replies.RejectCount;
            _startedAt = _clock.NowMs;
            _started = true;
        }
    }
}
