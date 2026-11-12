using System.Threading;
using TheVerge.Core.Ports;
using TheVerge.Core.Session;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Session;
using TheVerge.Network.Transport;

namespace TheVerge.Bot
{
    /// <summary>
    /// Каркас бота (PLAN Стадия 1, п. 15): собирает TcpConnection → NetworkSession → GameClient, крутит тики до конца
    /// соединения и говорит, чем всё кончилось, — сообщением и кодом выхода (<see cref="BotExitCode"/>). Вход на сервер
    /// появится в задачах 1.5–1.9. Пока бот, подключившись, сразу закрывает соединение сам: подключение, которое 5 с
    /// молчит, сервер считает атакой и блокирует адрес на 15 минут (NetState.Network.cs, DisconnectUnattachedSockets;
    /// docs/PLAN.md, риски Стадии 1).
    /// </summary>
    public sealed class BotRunner
    {
        /// <summary>Пауза между тиками бота, мс: как кадр Unity при 60 FPS (docs/TARGET.md §3.1).</summary>
        public const int TickIntervalMs = 16;

        private readonly IConnection _connection;
        private readonly IClock _clock;
        private readonly ILog _log;
        private readonly PacketLogWriter? _packetLog;

        public BotRunner(IConnection connection, IClock clock, ILog log, PacketLogWriter? packetLog)
        {
            _connection = connection;
            _clock = clock;
            _log = log;
            _packetLog = packetLog;
        }

        /// <param name="interrupt">Ctrl+C: соединение закрывается, бот возвращает <see cref="BotExitCode.Interrupted"/>.</param>
        public int Run(string host, int port, CancellationToken interrupt)
        {
            var endpoint = host + ":" + port;
            using (var stop = CancellationTokenSource.CreateLinkedTokenSource(interrupt))
            using (var session = new NetworkSession(_connection, new PacketDispatcher(_log), _log) { PacketLog = _packetLog })
            {
                _log.Info("Подключение к " + endpoint + "…");
                _connection.Connect(host, port, stop.Token);
                RunUntilClosed(session, new GameClient(session, _clock), stop);
                return Finish(session, endpoint, interrupt.IsCancellationRequested);
            }
        }

        private void RunUntilClosed(NetworkSession session, GameClient client, CancellationTokenSource stop)
        {
            var closing = false;
            while (true)
            {
                client.Tick(_clock.NowMs);
                if (session.IsClosed)
                {
                    return;
                }

                if (session.IsConnected && !closing)
                {
                    closing = true;
                    _log.Info("Подключено: сервер принял соединение. Вход на сервер появится в задачах 1.5–1.9, поэтому бот " +
                              "закрывает соединение сам: подключение, которое молчит 5 с, сервер блокирует на 15 минут.");
                    stop.Cancel();
                }

                // После отмены ждём только событие закрытия от фонового потока — оно приходит за миллисекунды.
                stop.Token.WaitHandle.WaitOne(stop.IsCancellationRequested ? 1 : TickIntervalMs);
            }
        }

        private int Finish(NetworkSession session, string endpoint, bool interrupted)
        {
            var detail = session.CloseDetail.Length > 0 ? " (" + session.CloseDetail + ")" : string.Empty;
            switch (session.CloseReason)
            {
                case ConnectionCloseReason.Cancelled when interrupted:
                    _log.Info("Остановлено по Ctrl+C: соединение закрыто.");
                    return BotExitCode.Interrupted;
                case ConnectionCloseReason.Cancelled:
                    _log.Info("Готово: соединение закрыто ботом.");
                    return BotExitCode.Success;
                case ConnectionCloseReason.ConnectFailed:
                    _log.Error("Не удалось подключиться к " + endpoint + detail + ". Запущен ли сервер, верны ли адрес и порт?");
                    return BotExitCode.ConnectFailed;
                case ConnectionCloseReason.ConnectTimeout:
                    _log.Error("Сервер " + endpoint + " не ответил на подключение вовремя. Запущен ли сервер, верен ли адрес?");
                    return BotExitCode.ConnectFailed;
                case ConnectionCloseReason.ServerClosed:
                    _log.Error("Сервер закрыл соединение. Причина — в консоли сервера.");
                    return BotExitCode.ConnectionLost;
                case ConnectionCloseReason.SocketError:
                    _log.Error("Ошибка сети: соединение потеряно" + detail + ".");
                    return BotExitCode.ConnectionLost;
                case ConnectionCloseReason.FramingFailed:
                    _log.Error("Сервер прислал поток, который клиент не понимает" + detail + ": соединение разорвано, дамп — выше.");
                    return BotExitCode.ProtocolError;
                default:
                    _log.Error("Соединение закончилось без известной причины (" + session.CloseReason + "): ошибка бота.");
                    return BotExitCode.InternalError;
            }
        }
    }
}
