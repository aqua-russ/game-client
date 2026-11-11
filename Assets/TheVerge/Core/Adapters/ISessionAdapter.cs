#nullable disable // долг прототипа: файл заменяется по docs/TECH-DEBT.md, разметку nullable не делаем
using System;

namespace TheVerge.Core
{
    /// <summary>
    /// Фасад подключения к серверу и авторизации (включая Headless Auto-Login).
    /// </summary>
    public interface ISessionAdapter
    {
        bool IsConnected { get; }
        bool HasEnteredWorld { get; }
        Serial LocalPlayerSerial { get; }

        void Connect(string host, int port, string account, string password, int characterSlot = 0);
        void ConnectWithCharacterCreation(string host, int port, string account, string password, CharacterCreationArgsDto creationArgs);
        void Login(string host, int port, string account, string password);
        void SelectCharacter(int slotIndex, string characterName = null);
        void CreateCharacter(CharacterCreationArgsDto args, int slotIndex = 0);
        void DeleteCharacter(int slotIndex);
        void Disconnect();

        event Action<CharacterListDto> OnCharacterListReceived;
        event Action<string> OnCharacterDeleteFailed;
        event Action<PlayerLoginConfirmDto> OnPlayerEnteredWorld;
        event Action<string> OnSessionError;
        event Action OnDisconnected;
    }
}
