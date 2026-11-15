using System.Text;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;

namespace TheVerge.Bot
{
    /// <summary>
    /// Как бот видел одного чужого моба (PLAN Стадия 3, п. 8; tools/check/others.sh): сколько шагов пришло, показано и
    /// выброшено перескоком; по режимам (пешком или верхом × шаг или бег) — средний интервал между приходами шагов и средняя
    /// длительность показа; догонялки, перескоки по причинам, появления. Интервал дольше полутора шагов — пауза (моб стоял),
    /// в темп не идёт; после перескока счёт интервалов начинается заново. Интервал до следующего прихода относится к
    /// предыдущему шагу: сервер ждёт после шага столько, сколько стоит этот шаг (Projects/Server/Mobiles/Mobile.cs, Move:
    /// _nextMovementTime += cost) — с его показом и сравниваем.
    /// </summary>
    public sealed class OtherStats
    {
        private static readonly string[] ModeNames = { "шаг пешком", "бег пешком", "шаг верхом", "бег верхом" };

        private readonly long[] _arrivalSum = new long[4];
        private readonly int[] _arrivalCount = new int[4];
        private readonly long[] _shownSum = new long[4];
        private readonly int[] _shownCount = new int[4];
        private readonly ushort _body;
        private long _lastArrived = -1;
        private int _lastMode;
        private int _lastDelayMs;
        private int _shown;
        private int _dropped;
        private int _catchUps;
        private int _appeared;
        private int _teleports;
        private int _jumps;
        private int _backlogs;

        public OtherStats(ushort body)
        {
            _body = body;
        }

        public void Add(ShownStep step)
        {
            _shown++;
            if (step.IsCatchUp)
            {
                _catchUps++;
            }

            int mode = (step.IsMounted ? 2 : 0) + (step.IsRunning ? 1 : 0);
            _shownSum[mode] += step.DurationMs;
            _shownCount[mode]++;
            long gap = step.ArrivedMs - _lastArrived;
            if (_lastArrived >= 0 && gap * 2 <= _lastDelayMs * 3)
            {
                _arrivalSum[_lastMode] += gap;
                _arrivalCount[_lastMode]++;
            }

            _lastArrived = step.ArrivedMs;
            _lastMode = mode;
            _lastDelayMs = StepTiming.StepDelayMs(step.IsMounted, step.IsRunning);
        }

        public void Add(MotionSnap snap)
        {
            _dropped += snap.DroppedSteps;
            _lastArrived = -1;
            switch (snap.Reason)
            {
                case SnapReason.Appeared: _appeared++; break;
                case SnapReason.Teleported: _teleports++; break;
                case SnapReason.Jumped: _jumps++; break;
                default: _backlogs++; break;
            }
        }

        /// <summary>Строка журнала: «Чужой 0x… тело 0x…: пришло N, показано N, выброшено N; бег пешком 200/200 мс ×N; …».</summary>
        public string Describe(Serial serial)
        {
            var text = new StringBuilder("Чужой " + serial + " тело 0x" + _body.ToString("X4") + ": пришло " + (_shown + _dropped) +
                                         ", показано " + _shown + ", выброшено " + _dropped);
            for (int mode = 0; mode < ModeNames.Length; mode++)
            {
                if (_shownCount[mode] == 0)
                {
                    continue;
                }

                var arrival = _arrivalCount[mode] > 0 ? (_arrivalSum[mode] / _arrivalCount[mode]).ToString() : "—";
                text.Append(mode == FirstMode() ? "; " : ", ").Append(ModeNames[mode]).Append(' ').Append(arrival).Append('/')
                    .Append(_shownSum[mode] / _shownCount[mode]).Append(" мс ×").Append(_shownCount[mode]);
            }

            return text.Append("; догонялок ").Append(_catchUps).Append(", перескоков ").Append(_teleports + _jumps + _backlogs)
                .Append(" (телепорт ").Append(_teleports).Append(", рывок ").Append(_jumps).Append(", отставание ").Append(_backlogs)
                .Append("), появлений ").Append(_appeared).Append('.').ToString();
        }

        private int FirstMode()
        {
            for (int mode = 0; mode < ModeNames.Length; mode++)
            {
                if (_shownCount[mode] > 0)
                {
                    return mode;
                }
            }

            return -1;
        }
    }
}
