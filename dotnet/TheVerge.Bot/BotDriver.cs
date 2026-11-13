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
        private LoginState _seen;

        public BotDriver(ILog log, BotLogin login)
        {
            _log = log;
            _login = login;
        }

        /// <summary>Почему бот не может идти дальше (нет нужного персонажа); пусто — может.</summary>
        public string Problem { get; private set; } = string.Empty;

        /// <summary>Цель бота достигнута: дальше вход клиента пока не умеет (растёт с задачами 1.9–1.10).</summary>
        public static bool IsGoal(LoginFlow flow) => flow.State == LoginState.EnteringWorld;

        public void Act(LoginFlow flow)
        {
            if (flow.State == _seen)
            {
                return;
            }

            _seen = flow.State;
            switch (flow.State)
            {
                case LoginState.ShardSelection:
                    ChooseShard(flow);
                    break;
                case LoginState.ConnectingGame:
                    _log.Info("Игровой сервер: " + flow.Redirect + ".");
                    break;
                case LoginState.CharacterSelection:
                    ChooseCharacter(flow.Characters);
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
