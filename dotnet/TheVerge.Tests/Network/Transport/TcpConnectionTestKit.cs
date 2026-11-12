using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TheVerge.Network.Transport;

namespace TheVerge.Tests.Network.Transport
{
    /// <summary>Общее для тестов TcpConnection: короткий таймаут, новое соединение, ожидание условия.</summary>
    internal static class TcpConnectionTestKit
    {
        public const int ShortTimeoutMs = 300;

        // Порт для молчащего сервера: подключение подставное, по сети ничего не уходит, номер ни на что не влияет.
        public const int SilentServerPort = 2593;

        public static TcpConnection NewConnection() => new TcpConnection(new CountingArrayPool(), ShortTimeoutMs, 1 << 20);

        /// <summary>
        /// Соединение с сервером, который не отвечает на подключение: подключение никогда не завершается. Раньше это
        /// изображали забитой очередью слушателя, но macOS в таком случае подключение принимает — трюк работал только
        /// в Linux. Подстановка одинакова на любой системе и не зависит от скорости машины.
        /// </summary>
        public static TcpConnection NewSilentServerConnection(int connectTimeoutMs) =>
            new TcpConnection(new CountingArrayPool(), connectTimeoutMs, 1 << 20, NeverAnswers, ReceiveFromSocket);

        /// <summary>
        /// Настоящее подключение, но чтение ждёт отмены <paramref name="cancelled"/> и возвращает «0 байт» — так ведёт себя
        /// сокет, когда его будит наше же закрытие.
        /// </summary>
        public static TcpConnection NewConnectionReadingEndAfter(CancellationToken cancelled) =>
            new TcpConnection(new CountingArrayPool(), ShortTimeoutMs, 1 << 20, ConnectToServer, (socket, buffer) =>
            {
                cancelled.WaitHandle.WaitOne();
                return 0;
            });

        private static Task ConnectToServer(Socket socket, string host, int port) => socket.ConnectAsync(host, port);

        private static int ReceiveFromSocket(Socket socket, byte[] buffer) => socket.Receive(buffer);

        private static Task NeverAnswers(Socket socket, string host, int port) => new TaskCompletionSource<bool>().Task;

        public static ConnectionCloseReason LastCloseReason(List<ConnectionEvents.Received> events)
        {
            Assert.That(events.Count, Is.GreaterThan(0), "событий нет");
            Assert.That(events[events.Count - 1].Kind, Is.EqualTo(InboundKind.Closed));
            return events[events.Count - 1].CloseReason;
        }

        public static bool WaitForState(IConnection connection, ConnectionState state) => WaitFor(() => connection.State == state);

        public static bool WaitFor(Func<bool> condition)
        {
            var watch = Stopwatch.StartNew();
            while (!condition() && watch.ElapsedMilliseconds < 5000)
            {
                Thread.Sleep(1);
            }

            return condition();
        }
    }
}
