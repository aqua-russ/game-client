using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;

namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Ответы сервера на шаги своего персонажа (PLAN Стадия 3, п. 6) — сюда их передают обработчики пакетов. Отказ (0x21)
    /// откатывает персонажа на клетку сервера; это не ошибка клиента — журнал пишет «Отказ сервера» (для разработчика и
    /// бота, игрок его не видит) и считает. Подтверждение не того шага (0x22) — рассинхрон: один запрос ресинхронизации
    /// (сервер его не ограничивает — Projects/UOContent/Network/Packets/IncomingPlayerPackets.cs, Resynchronize, «TODO:
    /// Throttle»), ответы на шаги до него пропускаются, пока сервер не пришлёт своё место (0x20). Своё место после
    /// телепорта, ресинхронизации и смены карты сервер шлёт в 0x20 — это не отказ.
    /// </summary>
    public sealed class MovementReplies
    {
        private readonly PlayerMotion _motion;
        private readonly IMovementSender _sender;
        private readonly ILog _log;

        public MovementReplies(PlayerMotion motion, IMovementSender sender, ILog log)
        {
            _motion = motion;
            _sender = sender;
            _log = log;
        }

        /// <summary>Сколько раз сервер отказал в шаге (старые отказы в отменённых шагах не считаются).</summary>
        public int RejectCount { get; private set; }

        /// <summary>Сколько раз клиент просил о ресинхронизации.</summary>
        public int ResyncCount { get; private set; }

        /// <summary>0x22: шаг выполнен; известность своего персонажа — в мир клиента.</summary>
        public void OnAck(byte sequence, byte notoriety)
        {
            if (_motion.IsResyncing || _motion.Confirm(sequence, notoriety))
            {
                return;
            }

            _log.Warning("Подтверждение шага " + sequence + " не совпало с ожидаемым — запрос ресинхронизации.");
            ResyncCount++;
            _motion.BeginResync();
            _sender.SendResync();
        }

        /// <summary>0x21: отказ в шаге; персонаж — на клетке и в направлении сервера.</summary>
        public void OnReject(byte sequence, Point3D position, Direction direction)
        {
            if (_motion.IsResyncing || !_motion.Reject(sequence, position, direction))
            {
                return;
            }

            RejectCount++;
            _log.Info("Отказ сервера: шаг " + sequence + ", персонаж на " + position + ".");
        }

        /// <summary>0x20 своего персонажа: сервер сбросил счёт шагов и сам поставил его сюда.</summary>
        public void OnServerPosition(Point3D position, Direction direction) => _motion.ResetToServer(position, direction);

        /// <summary>0xBF 0x08 на другую карту: шагов нет, пока не придёт новое место.</summary>
        public void OnMapChanged() => _motion.OnMapChanged();
    }
}
