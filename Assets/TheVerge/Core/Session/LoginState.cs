namespace TheVerge.Core.Session
{
    /// <summary>Где сейчас вход в игру (<see cref="LoginFlow"/>).</summary>
    public enum LoginState : byte
    {
        Idle = 0,

        /// <summary>Подключение к логин-серверу.</summary>
        ConnectingLogin = 1,

        /// <summary>Seed и логин отправлены, ждём список шардов или отказ.</summary>
        AwaitingShardList = 2,

        /// <summary>Вход закончился ошибкой (<see cref="LoginFlow.Error"/>).</summary>
        Failed = 100,

        /// <summary>Соединение закрыто по просьбе клиента.</summary>
        Closed = 101,
    }
}
