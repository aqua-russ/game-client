using System;
using System.Net.Sockets;

namespace TheVerge.Network.Transport
{
    /// <summary>
    /// Отправка без блокировки главного потока (AGENTS.md §2.3, исходящее, шаг 4): байты копируются в буфер, а в сокет
    /// уходят асинхронной отправкой ОС. Пока предыдущая отправка не закончилась, новые байты копятся во втором буфере;
    /// порядок сохраняется. Ошибка отправки — один раз сообщается владельцу, дальше отправка отказывает.
    /// </summary>
    internal sealed class SocketSender : IDisposable
    {
        private const int InitialBufferSize = 4 * 1024;

        private readonly Socket _socket;
        private readonly Action<SocketError> _onFailed;
        private readonly object _gate = new object();
        private readonly SocketAsyncEventArgs _args = new SocketAsyncEventArgs();
        private byte[] _pending = new byte[InitialBufferSize];
        private byte[] _inFlight = new byte[InitialBufferSize];
        private int _pendingCount;
        private int _inFlightOffset;
        private int _inFlightCount;
        private bool _sending;
        private bool _failed;

        public SocketSender(Socket socket, Action<SocketError> onFailed)
        {
            _socket = socket;
            _onFailed = onFailed;
            _args.Completed += OnCompleted;
        }

        /// <summary>false — отправка уже сломалась, байты не приняты.</summary>
        public bool Send(ReadOnlySpan<byte> data)
        {
            lock (_gate)
            {
                if (_failed)
                {
                    return false;
                }

                Append(data);
                if (!_sending)
                {
                    Pump();
                }

                return !_failed;
            }
        }

        public void Dispose()
        {
            _args.Completed -= OnCompleted;
            _args.Dispose();
        }

        private void Append(ReadOnlySpan<byte> data)
        {
            if (_pending.Length - _pendingCount < data.Length)
            {
                var grown = new byte[Math.Max(_pending.Length * 2, _pendingCount + data.Length)];
                Buffer.BlockCopy(_pending, 0, grown, 0, _pendingCount);
                _pending = grown;
            }

            data.CopyTo(_pending.AsSpan(_pendingCount));
            _pendingCount += data.Length;
        }

        // Вызывается под _gate. Синхронно завершённые отправки обрабатываются циклом, а не рекурсией.
        private void Pump()
        {
            while (true)
            {
                if (_inFlightOffset == _inFlightCount && !TakePending())
                {
                    _sending = false;
                    return;
                }

                _sending = true;
                _args.SetBuffer(_inFlight, _inFlightOffset, _inFlightCount - _inFlightOffset);
                if (!TryStart(out var pending) || pending || !Complete(_args))
                {
                    return;
                }
            }
        }

        private bool TryStart(out bool pending)
        {
            pending = false;
            try
            {
                pending = _socket.SendAsync(_args);
                return true;
            }
            catch (ObjectDisposedException)
            {
                // Сокет уже закрыт (закрытие соединения обогнало отправку): отправлять некуда.
                Fail(SocketError.Shutdown);
                return false;
            }
            catch (SocketException e)
            {
                Fail(e.SocketErrorCode);
                return false;
            }
        }

        private void OnCompleted(object? sender, SocketAsyncEventArgs args)
        {
            lock (_gate)
            {
                if (Complete(args))
                {
                    Pump();
                }
            }
        }

        private bool Complete(SocketAsyncEventArgs args)
        {
            if (args.SocketError != SocketError.Success || args.BytesTransferred == 0)
            {
                Fail(args.SocketError == SocketError.Success ? SocketError.ConnectionReset : args.SocketError);
                return false;
            }

            _inFlightOffset += args.BytesTransferred;
            return true;
        }

        private bool TakePending()
        {
            if (_pendingCount == 0)
            {
                return false;
            }

            var sent = _inFlight;
            _inFlight = _pending;
            _pending = sent;
            _inFlightCount = _pendingCount;
            _inFlightOffset = 0;
            _pendingCount = 0;
            return true;
        }

        private void Fail(SocketError error)
        {
            if (_failed)
            {
                return;
            }

            _failed = true;
            _sending = false;
            _pendingCount = 0;
            _onFailed(error);
        }
    }
}
