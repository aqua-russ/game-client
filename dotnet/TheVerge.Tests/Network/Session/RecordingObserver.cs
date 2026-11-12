using System.Collections.Generic;
using TheVerge.Core.Session;

namespace TheVerge.Tests.Network.Session
{
    /// <summary>Что сессия сообщила ядру о соединении — по порядку.</summary>
    internal sealed class RecordingObserver : IConnectionObserver
    {
        public List<string> Events { get; } = new List<string>();

        public void OnConnected() => Events.Add("connected");

        public void OnDisconnected(DisconnectReason reason, string detail) => Events.Add(reason + ":" + detail);
    }
}
