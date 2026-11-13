using TheVerge.Core.Ports;
using TheVerge.Core.Session;

namespace TheVerge.Bot
{
    /// <summary>
    /// Решения бота во время входа — то, что в игре делает человек: выбрать шард (первый в списке) и персонажа (по имени
    /// из настроек или первого; с --create-character — создать). Вызывается после каждого тика, поэтому выбор уходит
    /// серверу в конце следующего тика. Пишет в лог, докуда дошёл вход. Выбрать нельзя — <see cref="Problem"/>.
    /// </summary>
    internal sealed class BotDriver
    {
        private readonly ILog _log;
        private readonly BotLogin _login;
        private readonly WorldEntry _world;
        private LoginState _decided;

        public BotDriver(ILog log, BotLogin login, WorldEntry world)
        {
            _log = log;
            _login = login;
            _world = world;
        }

        /// <summary>Почему бот не может идти дальше (нет нужного персонажа); пусто — может.</summary>
        public string Problem { get; private set; } = string.Empty;

        /// <summary>Цель бота достигнута: персонаж в мире (ожидание и выход — задача 1.10).</summary>
        public static bool IsGoal(LoginFlow flow) => flow.State == LoginState.InWorld;

        /// <summary>Принять решение, если вход его ждёт (шард, персонаж). Каждое решение — один раз.</summary>
        public void Act(LoginFlow flow)
        {
            if (flow.State == _decided)
            {
                return;
            }

            switch (flow.State)
            {
                case LoginState.ShardSelection:
                    _decided = flow.State;
                    ChooseShard(flow);
                    break;
                case LoginState.CharacterSelection:
                    _decided = flow.State;
                    ChooseCharacter(flow.Characters);
                    break;
            }
        }

        /// <summary>
        /// Докуда дошёл вход — в лог. Подписка на смену состояния, а не опрос после тика: за один тик вход может пройти
        /// несколько состояний (подтверждение входа и конец загрузки мира приходят вместе).
        /// </summary>
        public void OnStateChanged(LoginFlow flow, LoginState state)
        {
            switch (state)
            {
                case LoginState.ConnectingGame:
                    _log.Info("Игровой сервер: " + flow.Redirect + ".");
                    break;
                case LoginState.WorldLoading:
                    _log.Info("Сервер подтвердил вход: персонаж " + _world.Player + ", карта " + _world.Player.Map + ".");
                    break;
                case LoginState.InWorld:
                    _log.Info("Персонаж в мире.");
                    break;
            }
        }

        private void ChooseShard(LoginFlow flow)
        {
            for (var i = 0; i < flow.Shards.Count; i++)
            {
                _log.Info("Шард " + (i + 1) + ": " + flow.Shards[i] + ", заполнен на " + flow.Shards[i].FullPercent + "%.");
            }

            _log.Info("Выбран шард «" + flow.Shards[0].Name + "».");
            flow.SelectShard(0);
        }

        private void ChooseCharacter(CharacterSelection characters)
        {
            _log.Info("Персонажи: " + BotCharacters.Describe(characters.Slots) + ".");
            var slot = _login.Character.Length > 0 ? characters.FindSlot(_login.Character) : BotCharacters.FirstOccupied(characters.Slots);
            if (slot >= 0)
            {
                _log.Info("Выбран персонаж «" + characters.Slots[slot].Name + "».");
                characters.Select(slot);
                return;
            }

            if (_login.CreateCharacter && _login.Character.Length > 0)
            {
                _log.Info("Создаётся персонаж «" + _login.Character + "» (город: " + characters.Cities[0] + ").");
                characters.Create(BotCharacters.NewCharacter(_login.Character));
                return;
            }

            Problem = _login.Character.Length > 0
                ? "на аккаунте нет персонажа «" + _login.Character + "». Проверьте имя или добавьте --create-character, чтобы создать его."
                : "на аккаунте нет персонажей. Создайте персонажа: --character <имя> --create-character.";
        }
    }
}
