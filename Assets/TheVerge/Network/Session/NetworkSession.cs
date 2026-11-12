using System;
using TheVerge.Core.Ports;
using TheVerge.Core.Session;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Framing;
using TheVerge.Network.Transport;

namespace TheVerge.Network.Session
{
    /// <summary>
    /// Реализация порта <see cref="INetworkSession"/> (ADR 0013 п. 7): по одному событию из очереди транспорта.
    /// Пакет — в <see cref="PacketDispatcher"/>, после чего его буфер возвращается в пул. Ошибка нарезки — контролируемый
    /// дисконнект с дампом в лог (AGENTS.md §3.5). Закрытие соединения — запись в лог. Исходящие тика — в
    /// <see cref="Outgoing"/>, в сокет одним вызовом <see cref="Flush"/>. Работает в главном потоке. Вход идёт через два
    /// соединения (логин-сервер, затем игровой), поэтому соединение сменяется (<see cref="Attach"/>): события прежнего
    /// больше не обрабатываются.
    /// </summary>
    public sealed class NetworkSession : INetworkSession, IDisposable
    {
        private IConnection? _connection;
        private readonly PacketDispatcher _dispatcher;
        private readonly ILog _log;
        private bool _sendFailureReported;

        public NetworkSession(IConnection connection, PacketDispatcher dispatcher, ILog log)
        {
            _connection = connection;
            _dispatcher = dispatcher;
            _log = log;
        }

        /// <summary>Сессия без соединения: его даёт <see cref="Attach"/> (ServerConnector).</summary>
        public NetworkSession(PacketDispatcher dispatcher, ILog log)
        {
            _dispatcher = dispatcher;
            _log = log;
        }

        public OutgoingBuffer Outgoing { get; } = new OutgoingBuffer();

        /// <summary>
        /// Лог пакетов (PLAN Стадия 1, п. 12): входящие — после нарезки, до обработки; исходящие — каждый пакет, когда тик
        /// передаёт их в сокет. null — лог выключен, и сессия ничего лишнего не делает.
        /// </summary>
        public PacketLogWriter? PacketLog { get; set; }

        /// <summary>Кому сообщать о подключении и конце соединения (вход в игру, Core/Session/LoginFlow). null — никому.</summary>
        public IConnectionObserver? Observer { get; set; }

        public bool IsConnected => _connection != null && _connection.State == ConnectionState.Connected;

        /// <summary>Соединение закончилось (закрыто или разорвано из-за сломанного потока); почему — <see cref="CloseReason"/>.</summary>
        public bool IsClosed => CloseReason != ConnectionCloseReason.None;

        public ConnectionCloseReason CloseReason { get; private set; }

        /// <summary>Подробности для человека: текст ошибки сокета или какой пакет сломал поток.</summary>
        public string CloseDetail { get; private set; } = string.Empty;

        public bool ProcessNext()
        {
            if (_connection == null || !_connection.TryReceive(out var item))
            {
                return false;
            }

            switch (item.Kind)
            {
                case InboundKind.Packet:
                    Handle(item);
                    break;
                case InboundKind.FramingFailed:
                    Disconnect(item.Error);
                    break;
                case InboundKind.Connected:
                    _log.Info("Подключено к серверу.");
                    PacketLog?.Connected();
                    Observer?.OnConnected();
                    break;
                case InboundKind.Closed:
                    OnClosed(item);
                    break;
            }

            return true;
        }

        public void Flush()
        {
            if (Outgoing.Count > 0)
            {
                SendPending();
            }

            PacketLog?.Flush();
        }

        /// <summary>
        /// Перейти на новое соединение: прежнее закрывается, его необработанные события отбрасываются, сессия снова
        /// не закрыта. Подключает новое соединение вызывающий (ServerConnector).
        /// </summary>
        public void Attach(IConnection connection)
        {
            _connection?.Dispose();
            _connection = connection;
            CloseReason = ConnectionCloseReason.None;
            CloseDetail = string.Empty;
            _sendFailureReported = false;
        }

        public void Dispose() => _connection?.Dispose();

        private void SendPending()
        {
            var pending = Outgoing.Pending;
            var sent = _connection != null && _connection.Send(pending);
            if (!sent && !_sendFailureReported)
            {
                _sendFailureReported = true;
                _log.Warning("Отправка не удалась: соединения нет. Исходящие пакеты пропущены (" + pending.Length + " байт).");
            }

            LogOutgoing(sent);
            Outgoing.Clear();
        }

        private void LogOutgoing(bool sent)
        {
            if (PacketLog == null)
            {
                return;
            }

            for (var i = 0; i < Outgoing.Count; i++)
            {
                PacketLog.Packet(PacketDirection.ClientToServer, Outgoing.PacketAt(i), sent);
            }
        }

        private void Handle(InboundEvent item)
        {
            PacketLog?.Packet(PacketDirection.ServerToClient, item.Packet, true);
            try
            {
                _dispatcher.Dispatch(item.Packet);
            }
            finally
            {
                item.Release();
            }
        }

        private void Disconnect(FramingError error)
        {
            var subCommand = error.Kind == FramingErrorKind.UnknownSubCommand ? " 0x" + error.SubCommand.ToString("X2") : string.Empty;
            _log.Error("Сломанный поток от сервера (" + error.Kind + "): пакет 0x" + error.Id.ToString("X2") + subCommand +
                       ", длина в заголовке " + error.DeclaredLength + ". Соединение разорвано. Дамп (" + error.Dump.Length +
                       " байт): " + PacketDump.ToHex(error.Dump));
            MarkClosed(ConnectionCloseReason.FramingFailed, "пакет 0x" + error.Id.ToString("X2") + subCommand + " (" + error.Kind + ")");
            _connection?.Dispose();
        }

        private void OnClosed(InboundEvent item)
        {
            MarkClosed(item.CloseReason, item.Reason);
            var message = "Соединение закрыто: " + item.CloseReason + (item.Reason.Length > 0 ? " (" + item.Reason + ")." : ".");
            if (item.CloseReason == ConnectionCloseReason.Cancelled)
            {
                _log.Info(message);
            }
            else
            {
                _log.Warning(message);
            }

            _connection?.Dispose();
        }

        private void MarkClosed(ConnectionCloseReason reason, string detail)
        {
            CloseReason = reason;
            CloseDetail = detail;
            PacketLog?.Closed(reason, detail);
            Observer?.OnDisconnected(DisconnectReasonOf(reason), detail);
        }

        private static DisconnectReason DisconnectReasonOf(ConnectionCloseReason reason)
        {
            switch (reason)
            {
                case ConnectionCloseReason.Cancelled:
                    return DisconnectReason.Requested;
                case ConnectionCloseReason.ConnectFailed:
                    return DisconnectReason.ConnectFailed;
                case ConnectionCloseReason.ConnectTimeout:
                    return DisconnectReason.ConnectTimeout;
                case ConnectionCloseReason.ServerClosed:
                    return DisconnectReason.ServerClosed;
                case ConnectionCloseReason.FramingFailed:
                    return DisconnectReason.ProtocolError;
                default:
                    return DisconnectReason.NetworkError;
            }
        }
    }
}
