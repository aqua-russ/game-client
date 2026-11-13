using System;

namespace TheVerge.Core.Session.Screens
{
    /// <summary>
    /// Один клиент на одну попытку входа, собранный снаружи (Bootstrap в Unity, тесты в памяти): вход, игровая сессия,
    /// повтор входа и ядро. <see cref="Dispose"/> закрывает соединение и всё, что открыл клиент.
    /// </summary>
    public sealed class LoginParts : IDisposable
    {
        private readonly IDisposable _owner;

        /// <param name="owner">Владелец соединения (сеть клиента): закрывается вместе с клиентом.</param>
        public LoginParts(LoginFlow flow, GameSession game, Reconnector reconnector, GameClient core, IDisposable owner)
        {
            Flow = flow;
            Game = game;
            Reconnector = reconnector;
            Core = core;
            _owner = owner;
        }

        public LoginFlow Flow { get; }

        public GameSession Game { get; }

        public Reconnector Reconnector { get; }

        public GameClient Core { get; }

        public void Dispose() => _owner.Dispose();
    }
}
