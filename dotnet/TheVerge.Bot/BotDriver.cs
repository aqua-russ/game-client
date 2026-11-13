using TheVerge.Core.Ports;
using TheVerge.Core.Session;

namespace TheVerge.Bot
{
    /// <summary>
    /// Решения бота во время входа — то, что в игре делает человек: выбрать шард (первый в списке). Вызывается после
    /// каждого тика, поэтому выбор уходит серверу в конце следующего тика. Пишет в лог, докуда дошёл вход.
    /// </summary>
    internal sealed class BotDriver
    {
        private readonly ILog _log;
        private LoginState _seen;

        public BotDriver(ILog log)
        {
            _log = log;
        }

        /// <summary>Цель бота достигнута: дальше вход клиента пока не умеет (растёт с задачами 1.7–1.10).</summary>
        public static bool IsGoal(LoginFlow flow) => flow.State == LoginState.AwaitingCharacterList;

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
                case LoginState.Redirected:
                    _log.Info("Игровой сервер: " + flow.Redirect + ".");
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
    }
}
