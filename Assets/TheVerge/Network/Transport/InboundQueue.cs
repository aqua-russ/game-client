using System.Collections.Generic;
using System.Threading;

namespace TheVerge.Network.Transport
{
    /// <summary>
    /// Очередь от фонового потока сокета к главному потоку (AGENTS.md §2.3). Пишет только фоновый поток, читает только
    /// главный. Считает байты пакетов в очереди: при пределе (ClientLimits.InboundQueueBytes, ADR 0013 п. 5) фоновый
    /// поток ждёт в <see cref="WaitForSpace"/> и не читает сокет.
    /// </summary>
    public sealed class InboundQueue
    {
        private readonly object _gate = new object();
        private readonly Queue<InboundEvent> _items = new Queue<InboundEvent>();
        private readonly ManualResetEventSlim _hasSpace = new ManualResetEventSlim(true);
        private readonly int _byteLimit;
        private int _queuedBytes;

        public InboundQueue(int byteLimit)
        {
            _byteLimit = byteLimit;
        }

        public int QueuedBytes
        {
            get
            {
                lock (_gate)
                {
                    return _queuedBytes;
                }
            }
        }

        public bool IsFull
        {
            get
            {
                lock (_gate)
                {
                    return _queuedBytes >= _byteLimit;
                }
            }
        }

        public void Enqueue(InboundEvent item)
        {
            lock (_gate)
            {
                _items.Enqueue(item);
                _queuedBytes += item.Length;
                UpdateSpace();
            }
        }

        public bool TryDequeue(out InboundEvent item)
        {
            lock (_gate)
            {
                if (_items.Count == 0)
                {
                    item = default;
                    return false;
                }

                item = _items.Dequeue();
                _queuedBytes -= item.Length;
                UpdateSpace();
                return true;
            }
        }

        /// <summary>Ждёт, пока в очереди есть место. false — ожидание отменено.</summary>
        public bool WaitForSpace(CancellationToken cancellation)
        {
            try
            {
                _hasSpace.Wait(cancellation);
                return true;
            }
            catch (System.OperationCanceledException)
            {
                return false;
            }
        }

        /// <summary>Выбросить всё, что не обработано, и вернуть буферы в пул (закрытие соединения).</summary>
        public void Clear()
        {
            lock (_gate)
            {
                while (_items.Count > 0)
                {
                    _items.Dequeue().Release();
                }

                _queuedBytes = 0;
                UpdateSpace();
            }
        }

        private void UpdateSpace()
        {
            if (_queuedBytes >= _byteLimit)
            {
                _hasSpace.Reset();
            }
            else
            {
                _hasSpace.Set();
            }
        }
    }
}
