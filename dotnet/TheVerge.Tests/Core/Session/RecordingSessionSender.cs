using System.Collections.Generic;
using TheVerge.Core.Ports;
using TheVerge.Core.Session;

namespace TheVerge.Tests.Core.Session
{
    /// <summary>Порт служебных пакетов в памяти: тест видит каждый вызов по порядку.</summary>
    internal sealed class RecordingSessionSender : ISessionSender
    {
        public List<string> Calls { get; } = new List<string>();

        public void SendClientVersion(ClientVersion version) => Calls.Add("version " + version);

        public void SendLanguage(string language) => Calls.Add("language " + language);

        public void SendViewRange(int range) => Calls.Add("range " + range);
    }
}
