using System.Collections.Generic;
using TheVerge.Network.Transport;

namespace TheVerge.Bot.Replay
{
    /// <summary>
    /// Фейковый сервер для CI (PLAN Стадия 1, п. 13): проигрывает записанный лог пакетов вместо настоящего ModernUO.
    /// Каждое новое соединение клиента получает следующее соединение из лога (логин-сервер, затем игровой).
    /// </summary>
    public sealed class ReplayServer
    {
        private readonly ReplayScript _script;
        private readonly List<string> _mismatches = new List<string>();
        private readonly List<ReplayConnection> _made = new List<ReplayConnection>();

        public ReplayServer(ReplayScript script)
        {
            _script = script;
        }

        /// <summary>Чем клиент разошёлся с логом: что ждали и что пришло. Пусто — всё по логу.</summary>
        public IReadOnlyList<string> Mismatches => _mismatches;

        /// <summary>Все пакеты клиента, которые ждал лог, пришли.</summary>
        public bool IsComplete
        {
            get
            {
                if (_made.Count != _script.Connections.Count)
                {
                    return false;
                }

                foreach (var connection in _made)
                {
                    if (!connection.IsComplete)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>Следующее соединение из лога; если в логе их больше нет — соединение, которое сразу закрывается.</summary>
        public IConnection NextConnection()
        {
            var index = _made.Count;
            var entries = index < _script.Connections.Count ? _script.Connections[index] : System.Array.Empty<Network.Diagnostics.PacketLogEntry>();
            if (index >= _script.Connections.Count)
            {
                Report("клиент открыл соединение " + (index + 1) + ", а в записи их " + _script.Connections.Count);
            }

            var connection = new ReplayConnection(entries, this);
            _made.Add(connection);
            return connection;
        }

        internal void Report(string mismatch) => _mismatches.Add(mismatch);
    }
}
