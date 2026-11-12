using System;
using System.Threading;
using TheVerge.Core.Ports;
using TheVerge.Network.Transport;

namespace TheVerge.Network.Session
{
    /// <summary>
    /// Порт <see cref="IServerConnector"/>: каждое подключение — новое соединение (одно IConnection — одно подключение),
    /// сессия переходит на него (NetworkSession.Attach), прежнее закрывается. Итог подключения приходит событием
    /// соединения в том же порядке, что и пакеты.
    /// </summary>
    public sealed class ServerConnector : IServerConnector
    {
        private readonly NetworkSession _session;
        private readonly Func<IConnection> _newConnection;
        private readonly CancellationToken _cancellation;

        /// <param name="cancellation">Отмена закрывает текущее соединение (Ctrl+C бота, выход из игры).</param>
        public ServerConnector(NetworkSession session, Func<IConnection> newConnection, CancellationToken cancellation)
        {
            _session = session;
            _newConnection = newConnection;
            _cancellation = cancellation;
        }

        public void Connect(string host, int port)
        {
            var connection = _newConnection();
            _session.Attach(connection);
            connection.Connect(host, port, _cancellation);
        }
    }
}
