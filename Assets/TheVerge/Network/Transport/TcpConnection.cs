using System;
using System.Buffers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TheVerge.Network.Transport
{
    /// <summary>
    /// TCP-подключение к серверу (PLAN Стадия 1, п. 4; AGENTS.md §2.3). Фоновый поток подключается (таймаут —
    /// ClientLimits.ConnectTimeoutMs), затем только читает сокет и отдаёт байты в <see cref="ReceivePipeline"/>; пока
    /// очередь полна, сокет не читает (ADR 0013 п. 5). Отправка — <see cref="SocketSender"/>, без блокировки.
    /// Отмена через CancellationToken и Dispose закрывают сокет: сервер видит закрытие, поток завершается.
    /// </summary>
    public sealed class TcpConnection : IConnection
    {
        // Сколько байт читать из сокета за раз; столько же у сервера буфер приёма (NetState.Network.cs, RecvBufferSize).
        private const int ReceiveBufferSize = 64 * 1024;

        private readonly int _connectTimeoutMs;
        private readonly Func<Socket, string, int, Task> _connect;
        private readonly Func<Socket, byte[], int> _receive;
        private readonly InboundQueue _inbound;
        private readonly ReceivePipeline _pipeline;
        private readonly Socket _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private readonly SocketSender _sender;
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly CancellationTokenRegistration _closeOnStop;
        private CancellationTokenRegistration _externalCancel;
        private Thread? _thread;
        private int _state;
        private int _disposed;
        private int _sendError;

        public TcpConnection(ArrayPool<byte> pool, int connectTimeoutMs, int inboundQueueBytes)
            : this(pool, connectTimeoutMs, inboundQueueBytes, ConnectSocket, ReceiveSocket)
        {
        }

        /// <summary>
        /// <paramref name="connect"/> начинает подключение сокета, <paramref name="receive"/> читает из него. В игре это всегда
        /// настоящие <see cref="Socket.ConnectAsync(string, int)"/> и <see cref="Socket.Receive(byte[])"/>; тесты
        /// подставляют подключение, которое не отвечает, и чтение с заданным порядком событий — молчащий сервер и гонку
        /// «закрытие против чтения» нельзя одинаково и надёжно изобразить средствами ОС на macOS и в Linux.
        /// </summary>
        internal TcpConnection(ArrayPool<byte> pool, int connectTimeoutMs, int inboundQueueBytes,
            Func<Socket, string, int, Task> connect, Func<Socket, byte[], int> receive)
        {
            _connectTimeoutMs = connectTimeoutMs;
            _connect = connect;
            _receive = receive;
            _inbound = new InboundQueue(inboundQueueBytes);
            _pipeline = new ReceivePipeline(_inbound, pool);
            _socket.NoDelay = true;
            _sender = new SocketSender(_socket, OnSendFailed);
            _closeOnStop = _stop.Token.Register(CloseSocket);
        }

        public ConnectionState State => (ConnectionState)Volatile.Read(ref _state);

        /// <summary>Сколько байт пакетов ждёт главного потока (диагностика).</summary>
        public int InboundQueuedBytes => _inbound.QueuedBytes;

        public void Connect(string host, int port, CancellationToken cancellation)
        {
            if (Interlocked.CompareExchange(ref _state, (int)ConnectionState.Connecting, (int)ConnectionState.Idle) !=
                (int)ConnectionState.Idle)
            {
                throw new InvalidOperationException("Соединение уже использовано: для нового подключения нужен новый TcpConnection.");
            }

            _externalCancel = cancellation.Register(_stop.Cancel);
            _thread = new Thread(() => Run(host, port)) { IsBackground = true, Name = "TheVerge.TcpConnection" };
            _thread.Start();
        }

        public void EnableCompression() => _pipeline.EnableCompression();

        public bool Send(ReadOnlySpan<byte> data) => State == ConnectionState.Connected && _sender.Send(data);

        public bool TryReceive(out InboundEvent item) => _inbound.TryDequeue(out item);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            _stop.Cancel();
            _thread?.Join();
            Volatile.Write(ref _state, (int)ConnectionState.Closed);
            _externalCancel.Dispose();
            _closeOnStop.Dispose();
            _stop.Dispose();
            _sender.Dispose();
            _socket.Dispose();
            _inbound.Clear();
        }

        private void Run(string host, int port)
        {
            var reason = ConnectionCloseReason.None;
            var detail = string.Empty;
            try
            {
                if (TryConnect(host, port, ref reason, ref detail))
                {
                    Volatile.Write(ref _state, (int)ConnectionState.Connected);
                    _inbound.Enqueue(InboundEvent.Connected());
                    ReadLoop(ref reason, ref detail);
                }
            }
            finally
            {
                Volatile.Write(ref _state, (int)ConnectionState.Closed);
                CloseSocket();
                _inbound.Enqueue(InboundEvent.Closed(reason, detail));
            }
        }

        private bool TryConnect(string host, int port, ref ConnectionCloseReason reason, ref string detail)
        {
            var connecting = _connect(_socket, host, port);
            // Если подключение брошено по таймауту или отмене, его ошибка (сокет закрыт) ожидаема: забираем её здесь.
            connecting.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            try
            {
                if (connecting.Wait(_connectTimeoutMs, _stop.Token))
                {
                    return true;
                }

                reason = ConnectionCloseReason.ConnectTimeout;
            }
            catch (OperationCanceledException)
            {
                reason = ConnectionCloseReason.Cancelled;
            }
            catch (AggregateException e)
            {
                reason = _stop.IsCancellationRequested ? ConnectionCloseReason.Cancelled : ConnectionCloseReason.ConnectFailed;
                detail = e.InnerException?.Message ?? e.Message;
            }

            return false;
        }

        private static Task ConnectSocket(Socket socket, string host, int port) => socket.ConnectAsync(host, port);

        private static int ReceiveSocket(Socket socket, byte[] buffer) => socket.Receive(buffer);

        private void ReadLoop(ref ConnectionCloseReason reason, ref string detail)
        {
            var buffer = new byte[ReceiveBufferSize];
            while (_inbound.WaitForSpace(_stop.Token))
            {
                if (!TryReceive(buffer, out var count, ref detail))
                {
                    reason = StopReason(ref detail);
                    return;
                }

                if (count == 0)
                {
                    // «0 байт» бывает и после нашего же закрытия: оно будит ждущее чтение. Тогда это отмена, а не сервер.
                    reason = _stop.IsCancellationRequested ? ConnectionCloseReason.Cancelled : ConnectionCloseReason.ServerClosed;
                    return;
                }

                _pipeline.Receive(new ReadOnlySpan<byte>(buffer, 0, count));
                if (_pipeline.IsFailed)
                {
                    reason = ConnectionCloseReason.FramingFailed;
                    return;
                }
            }

            reason = ConnectionCloseReason.Cancelled;
        }

        private bool TryReceive(byte[] buffer, out int count, ref string detail)
        {
            count = 0;
            try
            {
                count = _receive(_socket, buffer);
                return true;
            }
            catch (SocketException e)
            {
                detail = e.Message;
            }
            catch (ObjectDisposedException)
            {
                // Сокет закрыт отменой, Dispose или ошибкой отправки — причину назовёт StopReason.
            }

            return false;
        }

        private ConnectionCloseReason StopReason(ref string detail)
        {
            if (_stop.IsCancellationRequested)
            {
                return ConnectionCloseReason.Cancelled;
            }

            var sendError = (SocketError)Volatile.Read(ref _sendError);
            if (sendError != SocketError.Success)
            {
                detail = "ошибка отправки: " + sendError;
            }

            return ConnectionCloseReason.SocketError;
        }

        private void OnSendFailed(SocketError error)
        {
            Interlocked.Exchange(ref _sendError, (int)error);
            CloseSocket();
        }

        private void CloseSocket()
        {
            try
            {
                if (_socket.Connected)
                {
                    _socket.Shutdown(SocketShutdown.Both);
                }
            }
            catch (SocketException)
            {
                // Сервер уже закрыл соединение: сообщать ему о закрытии некуда, сокет всё равно закрывается ниже.
            }
            catch (ObjectDisposedException)
            {
                // Сокет уже закрыт другим путём (отмена и Dispose могут прийти одновременно).
                return;
            }

            _socket.Close();
        }
    }
}
