using System.Collections.Generic;
using System.Linq;
using TheVerge.Core.Ports;

namespace TheVerge.Tests.Network.Session
{
    /// <summary>Лог для тестов: запоминает записи с уровнем.</summary>
    internal sealed class RecordingLog : ILog
    {
        public List<(string Level, string Message)> Entries { get; } = new List<(string, string)>();

        public IEnumerable<string> Errors => Entries.Where(e => e.Level == "error").Select(e => e.Message);

        public IEnumerable<string> Warnings => Entries.Where(e => e.Level == "warning").Select(e => e.Message);

        public void Info(string message) => Entries.Add(("info", message));

        public void Warning(string message) => Entries.Add(("warning", message));

        public void Error(string message) => Entries.Add(("error", message));
    }
}
