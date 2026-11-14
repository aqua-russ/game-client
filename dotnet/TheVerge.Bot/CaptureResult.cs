using System.Collections.Generic;

namespace TheVerge.Bot
{
    /// <summary>Итог перевода записи посредника в лог пакетов (<see cref="CaptureConverter"/>).</summary>
    public sealed class CaptureResult
    {
        private readonly List<string> _errors = new List<string>();

        public int Connections { get; internal set; }

        public int ClientPackets { get; internal set; }

        public int ServerPackets { get; internal set; }

        /// <summary>Что не удалось разобрать — с местом (строка записи, соединение, направление, дамп); пусто — всё разобрано.</summary>
        public IReadOnlyList<string> Errors => _errors;

        internal void AddError(string error) => _errors.Add(error);
    }
}
