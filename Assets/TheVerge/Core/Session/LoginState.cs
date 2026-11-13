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

        /// <summary>Список шардов получен (<see cref="LoginFlow.Shards"/>), ждём выбора (<see cref="LoginFlow.SelectShard"/>).</summary>
        ShardSelection = 3,

        /// <summary>Шард выбран, ждём адрес игрового сервера.</summary>
        AwaitingRedirect = 4,

        /// <summary>Адрес игрового сервера получен (<see cref="LoginFlow.Redirect"/>).</summary>
        Redirected = 5,

        /// <summary>Подключение к игровому серверу по адресу из редиректа.</summary>
        ConnectingGame = 6,

        /// <summary>Ключ и вход на игровой сервер отправлены, ждём список персонажей.</summary>
        AwaitingCharacterList = 7,

        /// <summary>Список персонажей получен (<see cref="LoginFlow.Characters"/>), ждём выбора.</summary>
        CharacterSelection = 8,

        /// <summary>Персонаж выбран, ждём вход в мир.</summary>
        EnteringWorld = 9,

        /// <summary>Вход закончился ошибкой (<see cref="LoginFlow.Error"/>).</summary>
        Failed = 100,

        /// <summary>Соединение закрыто по просьбе клиента.</summary>
        Closed = 101,
    }
}
