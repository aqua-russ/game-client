using System;
using TheVerge.Core.Movement;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;

namespace TheVerge.Bot
{
    /// <summary>
    /// goto:x,y,run|walk (PLAN Стадия 3, п. 10; tools/check/scenes.sh): держать клавиши в сторону клетки — по диагонали, пока
    /// не совпадёт одна из осей, потом по прямой, — пока свой персонаж не встанет на неё по серверу. Маршрут — несколько
    /// таких точек подряд, отрезки выбираются свободными. Шаги делает темп (<see cref="StepPacer"/>) в тике GameClient, как
    /// у игрока. Клиент не шагает уже <see cref="StuckMs"/> — путь закрыт (по его же проверке шага): действие не удалось.
    /// </summary>
    internal sealed class GotoStep : IBotStep
    {
        private const int StuckMs = 3000;
        private const int SlackMs = 10000;

        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly IClock _clock;
        private readonly GotoRequest _request;
        private MoveKeys _held;
        private bool _started;
        private int _sentBefore;
        private int _rejectsBefore;
        private int _resyncsBefore;
        private int _lastSent;
        private long _lastSentAt;

        public GotoStep(BotWorld world, ILog log, IClock clock, string argument)
        {
            _world = world;
            _log = log;
            _clock = clock;
            GotoRequest.TryParse(argument, out _request);
        }

        public int TimeoutMs { get; private set; } = SlackMs;

        public string TimeoutProblem =>
            "персонаж не дошёл до (" + _request.X + ", " + _request.Y + ") за " + TimeoutMs / 1000 + " с, он в " +
            (_world.Entities.Player?.Position.ToString() ?? "неизвестно где") + ".";

        public string Failure { get; private set; } = string.Empty;

        public bool Poll()
        {
            var movement = _world.Movement;
            var player = _world.Entities.Player;
            if (movement == null)
            {
                Failure = "нужны файлы данных (--data): без карты шаг не предсказать.";
                return false;
            }

            if (player == null || !movement.Motion.IsPlaced)
            {
                return false;
            }

            if (!_started)
            {
                Start(movement, player.Position);
            }

            var motion = movement.Motion;
            if (CompassDirection.TryStepToward(motion.Position, _request.X, _request.Y, out var direction))
            {
                Hold(movement.Intent, CompassDirection.KeysFor(direction));
                CheckStuck(motion);
                return false;
            }

            Hold(movement.Intent, MoveKeys.None);
            if (motion.PendingSteps > 0 || player.Position.X != _request.X || player.Position.Y != _request.Y)
            {
                return false;
            }

            _log.Info(Report(movement));
            return true;
        }

        public void Finish() => Hold(_world.Movement?.Intent, MoveKeys.None);

        private void Start(BotMovement movement, Point3D position)
        {
            if (movement.Intent.IsRunning != _request.Run)
            {
                movement.Intent.ToggleRun();
            }

            int cells = Math.Max(Math.Abs(_request.X - position.X), Math.Abs(_request.Y - position.Y));
            TimeoutMs = cells * StepTiming.StepDelayMs(false, false) + SlackMs;
            _sentBefore = _lastSent = movement.Motion.MovesSent;
            _lastSentAt = _clock.NowMs;
            _rejectsBefore = movement.Replies.RejectCount;
            _resyncsBefore = movement.Replies.ResyncCount;
            _started = true;
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

        private void CheckStuck(PlayerMotion motion)
        {
            if (motion.MovesSent != _lastSent || motion.PendingSteps > 0 || motion.IsResyncing)
            {
                _lastSent = motion.MovesSent;
                _lastSentAt = _clock.NowMs;
                return;
            }

            if (_clock.NowMs - _lastSentAt >= StuckMs)
            {
                Failure = "путь закрыт: персонаж стоит в " + motion.Position + " и не может шагнуть к (" + _request.X + ", " +
                          _request.Y + ") — клиент не пускает (его проверка шага).";
            }
        }

        private string Report(BotMovement movement)
        {
            return "Маршрут до (" + _request.X + ", " + _request.Y + ")" + (_request.Run ? ", бег" : ", шаг") +
                   ": шагов " + (movement.Motion.MovesSent - _sentBefore) + ", отказов сервера " +
                   (movement.Replies.RejectCount - _rejectsBefore) + ", ресинхронизаций " + (movement.Replies.ResyncCount - _resyncsBefore) + ".";
        }
    }
}
