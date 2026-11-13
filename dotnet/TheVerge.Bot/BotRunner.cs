using System;
using System.Security.Cryptography;
using System.Threading;
using TheVerge.Core.Ports;
using TheVerge.Core.Session;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Packets.Account;
using TheVerge.Network.Session;
using TheVerge.Network.Transport;

namespace TheVerge.Bot
{
    /// <summary>
    /// Бот (PLAN Стадия 1, п. 15): собирает NetworkSession → GameClient и вход <see cref="LoginFlow"/>, крутит тики, пока
    /// вход не закончится и соединение не закроется, и говорит, чем всё кончилось, — сообщением и кодом выхода
    /// (<see cref="BotExitCode"/>). Seed и логин уходят сразу после подключения: подключение, которое молчит 5 с, сервер
    /// блокирует на 15 минут (NetState.Network.cs, DisconnectUnattachedSockets).
    /// </summary>
    public sealed class BotRunner
    {
        /// <summary>Пауза между тиками бота, мс: как кадр Unity при 60 FPS (docs/TARGET.md §3.1).</summary>
        public const int TickIntervalMs = 16;

        private readonly Func<IConnection> _newConnection;
        private readonly IClock _clock;
        private readonly ILog _log;
        private readonly PacketLogWriter? _packetLog;

        /// <param name="newConnection">Новое соединение на каждое подключение: вход идёт через логин- и игровой сервер.</param>
        public BotRunner(Func<IConnection> newConnection, IClock clock, ILog log, PacketLogWriter? packetLog)
        {
            _newConnection = newConnection;
            _clock = clock;
            _log = log;
            _packetLog = packetLog;
        }

        /// <param name="interrupt">Ctrl+C: соединение закрывается, бот возвращает <see cref="BotExitCode.Interrupted"/>.</param>
        public int Run(BotLogin login, CancellationToken interrupt)
        {
            using (var stop = CancellationTokenSource.CreateLinkedTokenSource(interrupt))
            {
                var dispatcher = new PacketDispatcher(_log);
                using (var session = new NetworkSession(dispatcher, _log) { PacketLog = _packetLog })
                {
                    var flow = new LoginFlow(new AccountSender(session), new ServerConnector(session, _newConnection, stop.Token), NewSeed());
                    AccountHandlers.Register(dispatcher, flow);
                    session.Observer = flow;
                    _log.Info("Подключение к " + login.Endpoint + "…");
                    flow.Start(login.Host, login.Port, login.Credentials);
                    var driver = new BotDriver(_log, login);
                    RunUntilDone(session, new GameClient(session, _clock), flow, driver, stop);
                    return BotOutcome.Report(flow, driver, login.Endpoint, interrupt.IsCancellationRequested, _log);
                }
            }
        }

        // Seed логин-сервера: любое число, кроме 0 (NetState.cs: нулевой сервер отключает).
        private static int NewSeed() => RandomNumberGenerator.GetInt32(1, int.MaxValue);

        // Решение бота (Act) пишет пакет в исходящие; он уходит в конце следующего тика. Поэтому «готово» проверяется после
        // тика и до решения: соединение закрывается, только когда последний пакет бота уже отправлен.
        private void RunUntilDone(NetworkSession session, GameClient client, LoginFlow flow, BotDriver driver, CancellationTokenSource stop)
        {
            while (true)
            {
                client.Tick(_clock.NowMs);
                var done = flow.IsFinished || BotDriver.IsGoal(flow) || driver.Problem.Length > 0;
                if (done && session.IsClosed)
                {
                    return;
                }

                if (!done)
                {
                    driver.Act(flow);
                }
                else if (!stop.IsCancellationRequested)
                {
                    // Вход закончился, а соединение ещё открыто — закрываем сами, чтобы не молчать на сервере.
                    stop.Cancel();
                }

                // После отмены ждём только событие закрытия от фонового потока — оно приходит за миллисекунды.
                stop.Token.WaitHandle.WaitOne(stop.IsCancellationRequested ? 1 : TickIntervalMs);
            }
        }
    }
}
