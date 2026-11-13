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
        private readonly Queue<InboundEvent> _afterSend = new Queue<InboundEvent>();
        private readonly Queue<InboundEvent[]> _replies = new Queue<InboundEvent[]>();

        public List<byte[]> Sent { get; } = new List<byte[]>();

        public bool AcceptsSend { get; set; } = true;

        /// <summary>false — не копировать отправленное (тест на выделение памяти: копия сама выделяет память).</summary>
        public bool KeepSent { get; set; } = true;

        public bool Disposed { get; private set; }

        public ConnectionState State { get; set; } = ConnectionState.Connected;

        public void Push(InboundEvent item) => _inbound.Enqueue(item);

        /// <summary>Событие придёт только после следующей отправки клиента — как ответ сервера на неё.</summary>
        public void PushAfterSend(InboundEvent item) => _afterSend.Enqueue(item);

        /// <summary>
        /// Ответ сервера на одну отправку клиента: каждая отправка открывает следующий ответ по порядку (диалог входа:
        /// логин → список шардов, выбор шарда → редирект).
        /// </summary>
        public void Reply(params InboundEvent[] items) => _replies.Enqueue(items);

        /// <summary>Куда просили подключиться («адрес:порт»); пусто — Connect не вызывали.</summary>
        public string ConnectedTo { get; private set; } = string.Empty;

        public bool CompressionEnabled { get; private set; }

        public void Connect(string host, int port, CancellationToken cancellation)
        {
            State = ConnectionState.Connecting;
            ConnectedTo = host + ":" + port;
        }

        public void EnableCompression() => CompressionEnabled = true;

        public bool Send(ReadOnlySpan<byte> data)
        {
            if (!AcceptsSend)
            {
                return false;
            }

            if (KeepSent)
            {
                Sent.Add(data.ToArray());
            }

            while (_afterSend.Count > 0)
            {
                _inbound.Enqueue(_afterSend.Dequeue());
            }

            if (_replies.Count > 0)
            {
                foreach (var item in _replies.Dequeue())
                {
                    _inbound.Enqueue(item);
                }
            }

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

            while (_afterSend.Count > 0)
            {
                _afterSend.Dequeue().Release();
            }

            while (_replies.Count > 0)
            {
                foreach (var item in _replies.Dequeue())
                {
                    item.Release();
                }
            }
        }
    }
}
