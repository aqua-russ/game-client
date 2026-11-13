namespace TheVerge.Core.Session.Screens
{
    /// <summary>
    /// Поля экрана входа: адрес сервера, логин, пароль. «Войти» можно, только если всё дойдёт до сервера без искажений
    /// (<see cref="LoginCredentials.Problem"/>). Пароль живёт только здесь и в памяти входа: ни в подсказках, ни в логе его нет.
    /// </summary>
    public sealed class LoginForm
    {
        private string _address = ClientLimits.DefaultServerAddress;
        private string _username = string.Empty;
        private string _password = string.Empty;

        public string Address
        {
            get => _address;
            set => Set(ref _address, value);
        }

        public string Username
        {
            get => _username;
            set => Set(ref _username, value);
        }

        public string Password
        {
            get => _password;
            set => Set(ref _password, value);
        }

        /// <summary>Растёт при каждой правке поля: экрану пора обновить подсказку и кнопку.</summary>
        public int Revision { get; private set; }

        /// <summary>Что мешает войти (первое по порядку полей); пусто — можно.</summary>
        public string Problem
        {
            get
            {
                var address = ServerAddress.Parse(_address, out _);
                if (address.Length > 0)
                {
                    return address;
                }

                var login = FieldProblem("логин", "Логин", _username);
                return login.Length > 0 ? login : FieldProblem("пароль", "Пароль", _password);
            }
        }

        public bool CanSubmit => Problem.Length == 0;

        /// <summary>Разобранный адрес; верен, когда <see cref="CanSubmit"/>.</summary>
        public ServerAddress ServerAddress
        {
            get
            {
                ServerAddress.Parse(_address, out var address);
                return address;
            }
        }

        /// <summary>Логин и пароль; только когда <see cref="CanSubmit"/> (иначе — исключение конструктора).</summary>
        public LoginCredentials Credentials => new LoginCredentials(_username, _password);

        private static string FieldProblem(string lower, string title, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Введите " + lower + ".";
            }

            var problem = LoginCredentials.Problem(value);
            return problem.Length == 0 ? string.Empty : title + ": " + char.ToLowerInvariant(problem[0]) + problem.Substring(1);
        }

        private void Set(ref string field, string value)
        {
            value = value ?? string.Empty;
            if (field == value)
            {
                return;
            }

            field = value;
            Revision++;
        }
    }
}
