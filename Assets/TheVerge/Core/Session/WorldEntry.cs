using TheVerge.Core.Ports;
using TheVerge.Core.World;

namespace TheVerge.Core.Session
{
    /// <summary>
    /// Вход в мир (PLAN Стадия 1, п. 9): подтверждение входа со своим персонажем, конец начальной загрузки мира и
    /// сведения о клиенте, которые сервер ждёт после входа. Состояние — общее с входом (<see cref="LoginProgress"/>).
    /// </summary>
    public sealed class WorldEntry
    {
        private readonly LoginProgress _progress;
        private readonly ISessionSender _sender;

        public WorldEntry(LoginProgress progress, ISessionSender sender)
        {
            _progress = progress;
            _sender = sender;
        }

        public PlayerState Player { get; private set; }

        /// <summary>Сервер подтвердил вход выбранным персонажем; дальше идёт начальный поток мира до <see cref="OnLoginComplete"/>.</summary>
        public void OnLoginConfirm(PlayerState player)
        {
            if (_progress.State != LoginState.EnteringWorld)
            {
                return;
            }

            Player = player;
            _progress.MoveTo(LoginState.WorldLoading);
        }

        /// <summary>Начальная загрузка мира кончилась: персонаж в мире. Сервер ждёт язык и дальность обзора клиента.</summary>
        public void OnLoginComplete()
        {
            if (_progress.State != LoginState.WorldLoading)
            {
                return;
            }

            _progress.MoveTo(LoginState.InWorld);
            _sender.SendLanguage(ClientLimits.Language);
            _sender.SendViewRange(ServerLimits.UpdateRange);
        }
    }
}
