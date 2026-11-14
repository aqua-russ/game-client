using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>Предупреждения и ошибки записи лога (предел размера, сбой записи) — в ошибки перевода записи.</summary>
    internal sealed class ResultLog : ILog
    {
        private readonly CaptureResult _result;

        public ResultLog(CaptureResult result)
        {
            _result = result;
        }

        public void Info(string message)
        {
        }

        public void Warning(string message) => _result.AddError(message);

        public void Error(string message) => _result.AddError(message);
    }
}
