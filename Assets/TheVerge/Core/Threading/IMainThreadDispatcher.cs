using System;

namespace TheVerge.Core
{
    /// <summary>
    /// Контракт потокобезопасного диспетчера для передачи сетевых коллбэков
    /// из фонового потока сокета в главный поток Unity (Main Thread).
    /// </summary>
    public interface IMainThreadDispatcher
    {
        void Enqueue(Action action);
    }
}
