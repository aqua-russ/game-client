using TheVerge.Core.Primitives;
using TheVerge.Core.World;

namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Куда придёт шаг своего персонажа (PLAN Стадия 3, п. 4): физика <see cref="MovementCheck"/> и проход сквозь живого
    /// моба — Projects/Server/Mobiles/Mobile.cs, CheckShove: на Фелукке только при полной выносливости, и сервер сразу снимает
    /// 10. Пока сервер не ответил на шаг сквозь моба, выносливость в статусе ещё старая, а сервер её уже снял, — второй раз
    /// подряд не проходим. Призраку проход разрешён и бесплатен (MoverState.FromMobile, CheckShove: !Alive).
    /// </summary>
    public sealed class StepPredictor
    {
        private readonly MovementCheck _check;
        private bool _shoveUnanswered;
        private byte _shoveSequence;

        public StepPredictor(MovementCheck check)
        {
            _check = check;
        }

        /// <summary>Клетка шага из <paramref name="from"/>; shoves — шаг сквозь моба (стоит выносливости).</summary>
        public bool TryPredict(Mobile player, Point3D from, Direction facing, out Point3D target, out bool shoves)
        {
            var mover = MoverState.FromMobile(player);
            shoves = false;
            if (!_check.CheckMovement(from, facing, mover, out int newZ))
            {
                shoves = !mover.CanShoveMobiles && CanShove(player)
                    && _check.CheckMovement(from, facing, MoverState.FromMobile(player, canShoveMobiles: true), out newZ);
                if (!shoves)
                {
                    target = from;
                    return false;
                }
            }

            var cell = from.GetStepPoint(facing);
            target = new Point3D(cell.X, cell.Y, (sbyte)newZ);
            return true;
        }

        /// <summary>Шаг сквозь моба ушёл с этим номером: следующий такой — только после ответа сервера на него.</summary>
        public void OnShoveSent(byte sequence)
        {
            _shoveUnanswered = true;
            _shoveSequence = sequence;
        }

        /// <summary>Сервер подтвердил шаг с этим номером.</summary>
        public void OnConfirmed(byte sequence)
        {
            if (_shoveUnanswered && sequence == _shoveSequence)
            {
                _shoveUnanswered = false;
            }
        }

        /// <summary>Сервер сам поставил персонажа: шагов в пути нет.</summary>
        public void Forget() => _shoveUnanswered = false;

        private bool CanShove(Mobile player)
        {
            var stamina = player.Status.Stam;
            return !_shoveUnanswered && stamina.Maximum > 0 && stamina.Current == stamina.Maximum;
        }
    }
}
