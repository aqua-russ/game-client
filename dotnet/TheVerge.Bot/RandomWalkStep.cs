using System;
using TheVerge.Core.Movement;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;

namespace TheVerge.Bot
{
    /// <summary>
    /// randomwalk:… (PLAN Стадия 3, п. 10, random-walk; tools/check/randomwalk.sh): держать клавиши случайной стороны 1–8 шагов,
    /// как игрок. Сторона выбирается только из тех, куда из предсказанной клетки пускает проверка шага клиента
    /// (<see cref="PlayerMotion.CanMove"/>) и которые не выводят из квадрата; впереди преграда или край квадрата — сразу новая
    /// сторона. Номер задаёт выбор: одна и та же прогулка. Шаги делает темп (<see cref="StepPacer"/>) в тике GameClient.
    /// Некуда шагнуть — бот застрял: действие не удалось.
    /// </summary>
    internal sealed class RandomWalkStep : IBotStep
    {
        private const int LongestSegment = 8;
        private const int RunInTen = 7;
        private const int SlackMs = 30000;

        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly RandomWalkRequest _request;
        private readonly Random _random;
        private readonly Direction[] _choices = new Direction[8];
        private PlayerMotion? _motion;
        private MoveKeys _held;
        private Direction _direction;
        private int _segmentLeft;
        private int _segments;
        private int _sentBefore;
        private int _rejectsBefore;
        private int _resyncsBefore;
        private int _runs;
        private int _walks;

        public RandomWalkStep(BotWorld world, ILog log, string argument)
        {
            _world = world;
            _log = log;
            RandomWalkRequest.TryParse(argument, out _request);
            _random = new Random(_request.Seed);
        }

        public int TimeoutMs => _request.Steps * StepTiming.StepDelayMs(false, false) + SlackMs;

        public string TimeoutProblem =>
            "за " + TimeoutMs / 1000 + " с пройдено " + Moves + " шагов из " + _request.Steps + ", персонаж в " +
            (_motion?.Position.ToString() ?? "неизвестно где") + ".";

        public string Failure { get; private set; } = string.Empty;

        private int Moves => _motion == null ? 0 : _motion.MovesSent - _sentBefore;

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

            if (_motion == null)
            {
                Start(movement);
            }

            if (Moves < _request.Steps)
            {
                Walk(movement);
                return false;
            }

            Hold(movement.Intent, MoveKeys.None);
            if (movement.Motion.PendingSteps > 0)
            {
                return false;
            }

            _log.Info(Report(movement));
            return true;
        }

        public void Finish()
        {
            Hold(_world.Movement?.Intent, MoveKeys.None);
            if (_motion != null)
            {
                _motion.Moved -= OnMoved;
            }
        }

        private void Start(BotMovement movement)
        {
            _motion = movement.Motion;
            _motion.Moved += OnMoved;
            _sentBefore = _motion.MovesSent;
            _rejectsBefore = movement.Replies.RejectCount;
            _resyncsBefore = movement.Replies.ResyncCount;
        }

        private void Walk(BotMovement movement)
        {
            var motion = movement.Motion;
            if (_segmentLeft > 0 && CanGo(motion, _direction))
            {
                return;
            }

            int count = 0;
            for (int i = 0; i < _choices.Length; i++)
            {
                if (CanGo(motion, (Direction)i))
                {
                    _choices[count++] = (Direction)i;
                }
            }

            if (count == 0)
            {
                Failure = "бот застрял в " + motion.Position + ": проверка шага не пускает ни в одну сторону внутри квадрата.";
                return;
            }

            _direction = _choices[_random.Next(count)];
            _segmentLeft = _random.Next(1, LongestSegment + 1);
            _segments++;
            SetRun(movement.Intent, _request.Pace == RandomWalkPace.Run || _request.Pace == RandomWalkPace.Mix && _random.Next(10) < RunInTen);
            Hold(movement.Intent, CompassDirection.KeysFor(_direction));
        }

        private bool CanGo(PlayerMotion motion, Direction direction)
        {
            var next = motion.Position.GetStepPoint(direction);
            return _request.Contains(next.X, next.Y) && motion.CanMove(direction);
        }

        private void OnMoved(StepMove move)
        {
            _segmentLeft--;
            if (move.IsRunning)
            {
                _runs++;
            }
            else
            {
                _walks++;
            }
        }

        private static void SetRun(MoveIntent intent, bool run)
        {
            if (intent.IsRunning != run)
            {
                intent.ToggleRun();
            }
        }

        private void Hold(MoveIntent? intent, MoveKeys keys)
        {
            if (intent == null || keys == _held)
            {
                return;
            }

            intent.Release(_held);
            intent.Press(keys);
            _held = keys;
        }

        private string Report(BotMovement movement) =>
            "Случайная ходьба: шагов " + Moves + " (бегом " + _runs + ", шагом " + _walks + "), отрезков " + _segments +
            ", отказов сервера " + (movement.Replies.RejectCount - _rejectsBefore) + ", ресинхронизаций " +
            (movement.Replies.ResyncCount - _resyncsBefore) + "; квадрат (" + _request.X0 + ", " + _request.Y0 + ")–(" +
            _request.X1 + ", " + _request.Y1 + "), персонаж в " + movement.Motion.Position + ".";
    }
}
