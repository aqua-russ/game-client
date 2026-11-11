using System;
using System.Collections.Generic;
using System.Threading;
using TheVerge.Network.Transport;

namespace TheVerge.Tests.Network.Session
{
    /// <summary>Соединение в памяти: тест кладёт события, как фоновый поток, и видит каждую отправку.</summary>
    internal sealed class FakeConnection : IConnection
    {
        private readonly Queue<InboundEvent> _inbound = new Queue<InboundEvent>();

        public List<byte[]> Sent { get; } = new List<byte[]>();

        public bool AcceptsSend { get; set; } = true;

        public bool Disposed { get; private set; }

        public ConnectionState State { get; set; } = ConnectionState.Connected;

        public void Push(InboundEvent item) => _inbound.Enqueue(item);

        public void Connect(string host, int port, CancellationToken cancellation) => State = ConnectionState.Connecting;

        public void EnableCompression()
        {
        }

        public bool Send(ReadOnlySpan<byte> data)
        {
            if (!AcceptsSend)
            {
                return false;
            }

            Sent.Add(data.ToArray());
            return true;
        }

        public bool TryReceive(out InboundEvent item) => _inbound.TryDequeue(out item);

        public void Dispose()
        {
            Disposed = true;
            State = ConnectionState.Closed;
            while (_inbound.Count > 0)
            {
                _inbound.Dequeue().Release();
            }
        }
    }
}
