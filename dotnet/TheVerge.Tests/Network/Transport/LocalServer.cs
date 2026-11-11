using System;
using System.Net;
using System.Net.Sockets;

namespace TheVerge.Tests.Network.Transport
{
    /// <summary>Тестовый сервер на 127.0.0.1 со свободным портом: принимает одно подключение и шлёт или читает байты.</summary>
    internal sealed class LocalServer : IDisposable
    {
        private readonly TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
        private Socket? _client;

        public LocalServer()
        {
            _listener.Start();
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public Socket Client => _client ?? throw new InvalidOperationException("клиент ещё не принят");

        public void Accept(int timeoutMs = 5000)
        {
            var accept = _listener.AcceptSocketAsync();
            if (!accept.Wait(timeoutMs))
            {
                throw new TimeoutException("клиент не подключился");
            }

            _client = accept.Result;
            _client.NoDelay = true;
            _client.ReceiveTimeout = timeoutMs;
        }

        public void SendInChunks(byte[] data, int chunk)
        {
            for (var offset = 0; offset < data.Length; offset += chunk)
            {
                Client.Send(data, offset, Math.Min(chunk, data.Length - offset), SocketFlags.None);
            }
        }

        public byte[] ReadExactly(int count)
        {
            var data = new byte[count];
            var read = 0;
            while (read < count)
            {
                var n = Client.Receive(data, read, count - read, SocketFlags.None);
                if (n == 0)
                {
                    throw new InvalidOperationException("клиент закрыл соединение раньше времени");
                }

                read += n;
            }

            return data;
        }

        /// <summary>true, если клиент закрыл соединение (чтение вернуло 0 байт).</summary>
        public bool SeesClientClosed()
        {
            var buffer = new byte[1];
            try
            {
                return Client.Receive(buffer) == 0;
            }
            catch (SocketException)
            {
                return true;
            }
        }

        public void CloseClient() => _client?.Close();

        public void Dispose()
        {
            _client?.Dispose();
            _listener.Stop();
        }
    }
}
