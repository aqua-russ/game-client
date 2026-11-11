using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace TheVerge.Tests.Network.Transport
{
    /// <summary>
    /// Адрес, который не отвечает на подключение: слушатель с крошечной очередью, заполненной подключениями, которые
    /// никто не принимает. Новое подключение повисает — так проверяется таймаут без внешней сети.
    /// </summary>
    internal sealed class HangingEndpoint : IDisposable
    {
        private const int ProbeTimeoutMs = 300;
        private const int MaxProbes = 64;

        private readonly Socket _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private readonly List<Socket> _held = new List<Socket>();

        public HangingEndpoint()
        {
            _listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            _listener.Listen(0);
            Port = ((IPEndPoint)_listener.LocalEndPoint!).Port;
            for (var i = 0; i < MaxProbes; i++)
            {
                var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                if (!probe.ConnectAsync(IPAddress.Loopback, Port).Wait(ProbeTimeoutMs))
                {
                    probe.Dispose();
                    return;
                }

                _held.Add(probe);
            }

            throw new InvalidOperationException("не удалось заполнить очередь подключений слушателя");
        }

        public int Port { get; }

        public void Dispose()
        {
            foreach (var socket in _held)
            {
                socket.Dispose();
            }

            _listener.Dispose();
        }
    }
}
