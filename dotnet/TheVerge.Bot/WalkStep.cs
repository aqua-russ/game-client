using System;
using System.Collections.Generic;
using TheVerge.Core.Movement;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;

namespace TheVerge.Bot
{
    /// <summary>
    /// walk:&lt;направление&gt;,&lt;шагов&gt;,run|walk (PLAN Стадия 3, п. 7; tools/check/walk.sh): держать клавиши направления,
    /// пока сервер не подтвердит столько шагов, и дождаться ответов на все отправленные. Бег — переключателем, как `~`.
    /// Шаги делает темп (<see cref="StepPacer"/>) в тике GameClient. В журнал — шаги, отказы сервера, ресинхронизации и
    /// паузы между отправленными шагами. Нужны файлы данных (--data).
    /// </summary>
    internal sealed class WalkStep : IBotStep
    {
        // Сверх времени самих шагов пешком: вход в действие, ответы сервера, разворот.
        private const int SlackMs = 10000;

        // Шагов сверх заказанных: поворот и те, что ещё в пути, когда клавиши отпущены.
        private const int ExtraSteps = 8;

        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly IClock _clock;
        private readonly WalkRequest _request;
        private readonly List<long> _sentAt;
        private bool _started;
        private bool _released;
        private int _confirmed;
        private int _sentBefore;
        private int _rejectsBefore;
        private int _resyncsBefore;
        private Point3D _confirmedAt;

        public WalkStep(BotWorld world, ILog log, IClock clock, string argument)
        {
            _world = world;
            _log = log;
            _clock = clock;
            WalkRequest.TryParse(argument, out _request);
            _sentAt = new List<long>(_request.Steps + ExtraSteps);
        }

        public int TimeoutMs => _request.Steps * StepTiming.StepDelayMs(false, false) + SlackMs;

        public string TimeoutProblem =>
            "сервер подтвердил " + _confirmed + " шагов из " + _request.Steps + " за " + TimeoutMs / 1000 + " с (путь закрыт или шаги не уходят).";

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

            if (player == null)
            {
                return false;
            }

            if (!_started)
            {
                Start(movement, player.Position);
                return false;
            }

            CountSent(movement.Motion);
            CountConfirmed(player.Position);
            if (!_released && _confirmed >= _request.Steps)
            {
                movement.Intent.Release(_request.HeldKeys);
                _released = true;
            }

            if (!_released || movement.Motion.PendingSteps > 0)
            {
                return false;
            }

            _log.Info(Report(movement));
            return true;
        }

        public void Finish()
        {
            if (_started && !_released)
            {
                _world.Movement?.Intent.Release(_request.HeldKeys);
            }
        }

        private void Start(BotMovement movement, Point3D position)
        {
            if (movement.Intent.IsRunning != _request.Run)
            {
                movement.Intent.ToggleRun();
            }

            movement.Intent.Press(_request.HeldKeys);
            _confirmedAt = position;
            _sentBefore = movement.Motion.MovesSent;
            _rejectsBefore = movement.Replies.RejectCount;
            _resyncsBefore = movement.Replies.ResyncCount;
            _started = true;
        }

        // Темп считается в тике до этого опроса: шаг, ушедший в тике, отмечен временем тика.
        private void CountSent(PlayerMotion motion)
        {
            while (_sentBefore + _sentAt.Count < motion.MovesSent)
            {
                _sentAt.Add(_clock.NowMs);
            }
        }

        // Мир клиента знает только клетки, подтверждённые сервером: сколько клеток прошёл персонаж — столько шагов подтверждено.
        private void CountConfirmed(Point3D position)
        {
            _confirmed += Math.Max(Math.Abs(position.X - _confirmedAt.X), Math.Abs(position.Y - _confirmedAt.Y));
            _confirmedAt = position;
        }

        private string Report(BotMovement movement)
        {
            var text = "Ходьба " + _request.Name + (_request.Run ? ", бег" : ", шаг") + (movement.Motion.IsMounted ? " верхом" : " пешком") +
                       ": шагов " + _confirmed + ", отказов сервера " + (movement.Replies.RejectCount - _rejectsBefore) +
                       ", ресинхронизаций " + (movement.Replies.ResyncCount - _resyncsBefore);
            if (_sentAt.Count < 2)
            {
                return text + ".";
            }

            long min = long.MaxValue, max = 0;
            for (int i = 1; i < _sentAt.Count; i++)
            {
                long gap = _sentAt[i] - _sentAt[i - 1];
                min = Math.Min(min, gap);
                max = Math.Max(max, gap);
            }

            long total = _sentAt[_sentAt.Count - 1] - _sentAt[0];
            int gaps = _sentAt.Count - 1;
            long perHundredSeconds = total > 0 ? gaps * 100000L / total : 0;
            return text + "; пауза между шагами в среднем " + total / gaps + " мс (" + perHundredSeconds / 100 + "," +
                   (perHundredSeconds % 100).ToString("D2") + " шага в секунду), от " + min + " до " + max + " мс.";
        }
    }
}
