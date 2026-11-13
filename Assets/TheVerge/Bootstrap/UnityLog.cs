using TheVerge.Core.Ports;
using UnityEngine;

namespace TheVerge.Bootstrap
{
    /// <summary>Лог ядра и сети в консоль Unity. Пароль в лог не попадает: его не пишет ни ядро, ни сеть.</summary>
    public sealed class UnityLog : ILog
    {
        private const string Prefix = "[The Verge] ";

        public void Info(string message) => Debug.Log(Prefix + message);

        public void Warning(string message) => Debug.LogWarning(Prefix + message);

        public void Error(string message) => Debug.LogError(Prefix + message);
    }
}
