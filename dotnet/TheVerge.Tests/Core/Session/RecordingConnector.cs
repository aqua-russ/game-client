using System.Collections.Generic;
using TheVerge.Core.Ports;

namespace TheVerge.Tests.Core.Session
{
    /// <summary>Порт подключения в памяти: куда и сколько раз просили подключиться.</summary>
    internal sealed class RecordingConnector : IServerConnector
    {
        public List<string> Endpoints { get; } = new List<string>();

        public void Connect(string host, int port) => Endpoints.Add(host + ":" + port);
    }
}
