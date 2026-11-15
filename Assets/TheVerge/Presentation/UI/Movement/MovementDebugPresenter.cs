using System;
using TheVerge.Core.Movement;
using TheVerge.Core.Session;

namespace TheVerge.Presentation.UI.Movement
{
    /// <summary>
    /// Экран отладки движения (PLAN Стадия 3, п. 9; критерий «Unity»): свой персонаж — клетка и Z, режим и темп, ждущие
    /// шаги, задержка подтверждения, отклик на нажатие, отказы и ресинхронизации, откаты и перестановки; чужие — сколько
    /// видно, догонялки и перескоки по причинам. Все числа считает ядро (<see cref="StepAckLatency"/>,
    /// <see cref="StepPacer"/>, <see cref="PlayerStepShow"/>, <see cref="OthersMotion"/>, <see cref="MovementReplies"/>);
    /// здесь — только текст. Обновляется <see cref="RefreshesPerSecond"/> раз в секунду, строка собирается заново, только если
    /// её числа изменились.
    /// </summary>
    public sealed class MovementDebugPresenter : IDisposable
    {
        public const int RefreshesPerSecond = 4;

        private const string None = "—";

        private readonly PlayerMovementParts _player;
        private readonly OthersMotion _others;
        private readonly MovementDebugView _view;
        private readonly ChangedNumbers _cell = new ChangedNumbers();
        private readonly ChangedNumbers _mode = new ChangedNumbers();
        private readonly ChangedNumbers _pending = new ChangedNumbers();
        private readonly ChangedNumbers _ack = new ChangedNumbers();
        private readonly ChangedNumbers _response = new ChangedNumbers();
        private readonly ChangedNumbers _answers = new ChangedNumbers();
        private readonly ChangedNumbers _jumps = new ChangedNumbers();
        private readonly ChangedNumbers _othersCount = new ChangedNumbers();
        private readonly ChangedNumbers _othersSnaps = new ChangedNumbers();
        private long _nextRefreshMs;

        public MovementDebugPresenter(PlayerMovementParts player, OthersMotion others, MovementDebugView view)
        {
            _player = player;
            _others = others;
            _view = view;
            if (!player.IsAvailable)
            {
                ShowNoWalking();
            }

            _view.Show(true);
        }

        /// <param name="now">Часы ядра (IClock).</param>
        public void Update(long now)
        {
            if (now < _nextRefreshMs)
            {
                return;
            }

            _nextRefreshMs = now + 1000 / RefreshesPerSecond;
            if (_player.IsAvailable)
            {
                RefreshPosition();
                RefreshAnswers();
            }

            RefreshOthers();
        }

        public void Dispose() => _view.Show(false);

        private void RefreshPosition()
        {
            var motion = _player.Motion;
            var cell = motion.Position;
            if (_cell.Changed(cell.X, cell.Y, cell.Z))
            {
                Set(MovementDebugLine.Cell, "Клетка: " + cell.X.ToString() + ", " + cell.Y.ToString() + "   Z: " + cell.Z.ToString());
            }

            bool mounted = motion.IsMounted;
            bool running = motion.IsRunning;
            if (_mode.Changed(mounted ? 1 : 0, running ? 1 : 0))
            {
                Set(MovementDebugLine.Mode, "Режим: " + (mounted ? "верхом" : "пешком") + ", " + (running ? "бег" : "шаг") + " — " +
                    StepTiming.StepDelayMs(mounted, running).ToString() + " мс на клетку");
            }

            int pending = motion.PendingSteps;
            if (_pending.Changed(pending))
            {
                Set(MovementDebugLine.Pending, "Неподтверждённых шагов: " + pending.ToString() + " из " +
                    ServerLimits.MaxUnacknowledgedSteps.ToString());
            }

            int response = _player.Pacer.LastResponseMs;
            if (_response.Changed(response))
            {
                Set(MovementDebugLine.Response, "Отклик на нажатие: " + (response < 0 ? None : response.ToString() + " мс"));
            }
        }

        private void RefreshAnswers()
        {
            var latency = _player.Latency;
            if (_ack.Changed(latency.Count, latency.LastMs, latency.AverageMs, latency.MaxMs))
            {
                Set(MovementDebugLine.Ack, latency.Count == 0
                    ? "Подтверждение шага: " + None
                    : "Подтверждение шага: " + latency.LastMs.ToString() + " мс (среднее " + latency.AverageMs.ToString() +
                      ", наибольшее " + latency.MaxMs.ToString() + ")");
            }

            var replies = _player.Replies;
            if (_answers.Changed(replies.RejectCount, replies.ResyncCount))
            {
                Set(MovementDebugLine.Answers, "Отказы сервера: " + replies.RejectCount.ToString() + "   ресинхронизации: " +
                    replies.ResyncCount.ToString());
            }

            var show = _player.Show;
            if (_jumps.Changed(show.RollbackCount, show.RepositionCount))
            {
                Set(MovementDebugLine.Jumps, "Откаты: " + show.RollbackCount.ToString() + "   перестановки сервером: " +
                    show.RepositionCount.ToString());
            }
        }

        private void RefreshOthers()
        {
            if (_othersCount.Changed(_others.TrackedCount, _others.CatchUpCount))
            {
                Set(MovementDebugLine.Others, "Видно: " + _others.TrackedCount.ToString() + "   догонялок: " +
                    _others.CatchUpCount.ToString());
            }

            int teleported = _others.SnapsOf(SnapReason.Teleported);
            int jumped = _others.SnapsOf(SnapReason.Jumped);
            int backlog = _others.SnapsOf(SnapReason.Backlog);
            if (_othersSnaps.Changed(teleported, jumped, backlog))
            {
                Set(MovementDebugLine.OthersSnaps, "Перескоки: телепорт " + teleported.ToString() + "   рывок " + jumped.ToString() +
                    "   отставание " + backlog.ToString());
            }
        }

        private void ShowNoWalking()
        {
            Set(MovementDebugLine.Cell, "Ходьба недоступна: нет файлов данных");
            Set(MovementDebugLine.Mode, string.Empty);
            Set(MovementDebugLine.Pending, string.Empty);
            Set(MovementDebugLine.Ack, string.Empty);
            Set(MovementDebugLine.Response, string.Empty);
            Set(MovementDebugLine.Answers, string.Empty);
            Set(MovementDebugLine.Jumps, string.Empty);
        }

        private void Set(MovementDebugLine line, string text) => _view.SetLine(line, text);
    }
}
