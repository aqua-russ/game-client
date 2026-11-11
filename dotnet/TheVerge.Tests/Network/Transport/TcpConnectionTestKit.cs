using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using TheVerge.Network.Transport;

namespace TheVerge.Tests.Network.Transport
{
    /// <summary>Общее для тестов TcpConnection: короткий таймаут, новое соединение, ожидание условия.</summary>
    internal static class TcpConnectionTestKit
    {
        public const int ShortTimeoutMs = 300;

        public static TcpConnection NewConnection() => new TcpConnection(new CountingArrayPool(), ShortTimeoutMs, 1 << 20);

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
