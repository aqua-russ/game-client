namespace TheVerge.Core.Session.Screens
{
    /// <summary>Какой экран входа показан (<see cref="LoginScreens"/>).</summary>
    public enum LoginScreen : byte
    {
        /// <summary>Адрес сервера, логин и пароль.</summary>
        Login = 0,

        /// <summary>Ждём сервер: строка статуса (<see cref="LoginScreens.Status"/>) и кнопка «Отмена».</summary>
        Waiting = 1,

        /// <summary>Список шардов.</summary>
        Shards = 2,

        /// <summary>Список персонажей.</summary>
        Characters = 3,

        /// <summary>Окно создания персонажа.</summary>
        CreateCharacter = 4,

        /// <summary>Персонаж в мире: строка статуса и кнопка «Выйти».</summary>
        InWorld = 5,
    }
}
