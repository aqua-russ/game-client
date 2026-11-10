using System;
using System.Collections.Concurrent;
using TheVerge.Core;
using UnityEngine;

namespace TheVerge.Presentation
{
    /// <summary>
    /// Потокобезопасный диспетчер для передачи сетевых коллбэков в главный поток Unity (Main Thread).
    /// Выполняет накопившиеся действия в методе Update() без лишних аллокаций.
    /// </summary>
    public sealed class MainThreadDispatcher : MonoBehaviour, IMainThreadDispatcher
    {
        private static readonly ConcurrentQueue<Action> ExecutionQueue = new ConcurrentQueue<Action>();

        public void Enqueue(Action action)
        {
            if (action == null) return;
            ExecutionQueue.Enqueue(action);
        }

        private void Update()
        {
            while (ExecutionQueue.TryDequeue(out var action))
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
    }
}
