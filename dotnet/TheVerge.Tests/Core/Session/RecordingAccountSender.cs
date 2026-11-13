using System.Collections.Generic;
using TheVerge.Core.Ports;
using TheVerge.Core.Session;

namespace TheVerge.Tests.Core.Session
{
    /// <summary>Порт отправки входа в памяти: тест видит каждый вызов по порядку.</summary>
    internal sealed class RecordingAccountSender : IAccountSender
    {
        public List<string> Calls { get; } = new List<string>();

        public int Seed { get; private set; }

        public ClientVersion Version { get; private set; }

        public LoginCredentials Credentials { get; private set; }

        public void SendLoginSeed(int seed, ClientVersion version)
        {
            Calls.Add("seed");
            Seed = seed;
            Version = version;
        }

        public int ShardIndex { get; private set; } = -1;

        public void SendSelectShard(int index)
        {
            Calls.Add("shard");
            ShardIndex = index;
        }

        public void SendAccountLogin(LoginCredentials credentials)
        {
            Calls.Add("login");
            Credentials = credentials;
        }
    }
}
