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

        public int AuthKey { get; private set; }

        public void SendGameLogin(int authKey, LoginCredentials credentials)
        {
            Calls.Add("game");
            AuthKey = authKey;
            Credentials = credentials;
        }

        public int CharacterSlot { get; private set; } = -1;

        public string CharacterName { get; private set; } = string.Empty;

        public void SendPlayCharacter(int slot, string name)
        {
            Calls.Add("play");
            CharacterSlot = slot;
            CharacterName = name;
        }

        public NewCharacter? Created { get; private set; }

        public int DeletedSlot { get; private set; } = -1;

        public void SendCreateCharacter(NewCharacter character, int slot)
        {
            Calls.Add("create");
            Created = character;
            CharacterSlot = slot;
        }

        public void SendDeleteCharacter(int slot)
        {
            Calls.Add("delete");
            DeletedSlot = slot;
        }

        public void SendAccountLogin(LoginCredentials credentials)
        {
            Calls.Add("login");
            Credentials = credentials;
        }
    }
}
