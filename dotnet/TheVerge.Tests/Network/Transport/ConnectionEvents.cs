using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using TheVerge.Network.Transport;

namespace TheVerge.Tests.Network.Transport
{
    /// <summary>Забирает события соединения так, как это будет делать главный поток: опросом очереди.</summary>
    internal static class ConnectionEvents
    {
        public sealed class Received
        {
            public InboundKind Kind;
            public byte[] Packet = Array.Empty<byte>();
            public string Reason = string.Empty;
            public ConnectionCloseReason CloseReason;
        }

        public static List<Received> Collect(IConnection connection, Func<List<Received>, bool> done, int timeoutMs = 5000)
        {
            var events = new List<Received>();
            var watch = Stopwatch.StartNew();
            while (!done(events) && watch.ElapsedMilliseconds < timeoutMs)
            {
                if (!connection.TryReceive(out var item))
                {
                    Thread.Sleep(1);
                    continue;
                }

                events.Add(new Received { Kind = item.Kind, Packet = item.Packet.ToArray(), Reason = item.Reason, CloseReason = item.CloseReason });
                item.Release();
            }

            return events;
        }

        public static Func<List<Received>, bool> Count(int count) => events => events.Count >= count;

        public static Func<List<Received>, bool> Until(InboundKind kind) =>
            events => events.Count > 0 && events[events.Count - 1].Kind == kind;
    }
}
