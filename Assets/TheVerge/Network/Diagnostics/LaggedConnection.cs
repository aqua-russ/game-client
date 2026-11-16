using System;
using System.Buffers;
using System.Threading;
using TheVerge.Core.Diagnostics;
using TheVerge.Core.Ports;
using TheVerge.Network.Transport;

namespace TheVerge.Network.Diagnostics
{
    /// <summary>
    /// Режим «симуляция пинга» (PLAN Стадия 3, п. 9; ADR 0020 п. 9): соединение-обёртка, которое задерживает события от
    /// сервера и отправку клиента по настройкам <see cref="LagSimulation"/> (<see cref="DelayLine{T}"/> на каждое
    /// направление), не меняя ни байта и не переставляя пакеты: протокол тот же. Выключено и очередь пуста — всё проходит в
    /// тот же вызов; выключили на ходу — сначала выходит то, что уже в очереди. Отложенная отправка уходит при следующем
    /// обращении к соединению: GameClient каждый тик спрашивает события (<see cref="TryReceive"/>), так что точность —
    /// тик. Подключение, закрытие и ошибка нарезки идут в одной очереди с пакетами, по порядку. Работает в главном потоке,
    /// как и <see cref="IConnection"/> для сессии; годится и боту (задача 3.12).
    /// </summary>
    public sealed class LaggedConnection : IConnection
    {
        private readonly IConnection _inner;
        private readonly IClock _clock;
        private readonly LagSimulation _lag;
        private readonly ArrayPool<byte> _pool;
        private readonly DelayLine<InboundEvent> _incoming;
        private readonly DelayLine<OutgoingChunk> _outgoing;
        private long _incomingHoldUntilMs = long.MinValue;
        private bool _disposed;

        /// <param name="pool">Откуда брать буферы для копий задержанной отправки.</param>
        public LaggedConnection(IConnection inner, IClock clock, LagSimulation lag, Random random, ArrayPool<byte> pool)
        {
            _inner = inner;
            _clock = clock;
            _lag = lag;
            _pool = pool;
            _incoming = new DelayLine<InboundEvent>(random);
            _outgoing = new DelayLine<OutgoingChunk>(random);
        }

        public ConnectionState State => _inner.State;

        public void Connect(string host, int port, CancellationToken cancellation) => _inner.Connect(host, port, cancellation);

        public void EnableCompression() => _inner.EnableCompression();

        public bool Send(ReadOnlySpan<byte> data)
        {
            long now = _clock.NowMs;
            SendDue(now);
            var lag = _lag.Outgoing;
            if (_outgoing.Count == 0 && lag.IsNone)
            {
                return _inner.Send(data);
            }

            if (_disposed || _inner.State != ConnectionState.Connected)
            {
                return false;
            }

            var buffer = _pool.Rent(data.Length);
            data.CopyTo(buffer);
            _outgoing.Push(new OutgoingChunk(buffer, data.Length), now, lag);
            SendDue(now);
            return true;
        }

        public bool TryReceive(out InboundEvent item)
        {
            long now = _clock.NowMs;
            SendDue(now);
            int spike = _lag.TakeIncomingSpikeMs();
            if (spike > 0)
            {
                _incomingHoldUntilMs = Math.Max(_incomingHoldUntilMs, now + spike);
            }

            var lag = _lag.Incoming;
            if (_incoming.Count == 0 && lag.IsNone && now >= _incomingHoldUntilMs)
            {
                return _inner.TryReceive(out item);
            }

            while (_inner.TryReceive(out var arrived))
            {
                _incoming.Push(arrived, now, lag, _incomingHoldUntilMs);
            }

            return _incoming.TryPop(now, out item);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            while (_incoming.TryPopAny(out var item))
            {
                item.Release();
            }

            while (_outgoing.TryPopAny(out var chunk))
            {
                _pool.Return(chunk.Buffer);
            }

            _inner.Dispose();
        }

        private void SendDue(long now)
        {
            while (_outgoing.TryPop(now, out var chunk))
            {
                _inner.Send(new ReadOnlySpan<byte>(chunk.Buffer, 0, chunk.Length));
                _pool.Return(chunk.Buffer);
            }
        }

        private readonly struct OutgoingChunk
        {
            public OutgoingChunk(byte[] buffer, int length)
            {
                Buffer = buffer;
                Length = length;
            }

            public byte[] Buffer { get; }

            public int Length { get; }
        }
    }
}
