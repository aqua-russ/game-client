using System;
using System.Globalization;
using System.IO;
using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>Лог бота: строка «время уровень сообщение», уровни по-русски. Пишет в переданный поток (обычно консоль).</summary>
    public sealed class ConsoleLog : ILog
    {
        private readonly TextWriter _output;

        public ConsoleLog(TextWriter output)
        {
            _output = output;
        }

        public void Info(string message) => Write("ИНФО", message);

        public void Warning(string message) => Write("ВНИМАНИЕ", message);

        public void Error(string message) => Write("ОШИБКА", message);

        private void Write(string level, string message)
        {
            // Строки — через «\n» на любой ОС; время — по часам компьютера, чтобы сверять с логом сервера.
            _output.Write(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " " + level + " " + message + "\n");
            _output.Flush();
        }
    }
}
