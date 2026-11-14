using System.IO;
using System.Text;

namespace TheVerge.Bot
{
    /// <summary>--convert-capture: запись посредника → лог пакетов, итог строкой, ошибки списком (PLAN Стадия 0, п. 6).</summary>
    internal static class CaptureCommand
    {
        private const int MaxListed = 20;

        public static int Run(string capturePath, string logPath, TextWriter output)
        {
            if (!File.Exists(capturePath))
            {
                output.WriteLine("ОШИБКА: нет файла записи «" + capturePath + "».");
                return BotExitCode.BadArguments;
            }

            CaptureResult result;
            using (var capture = new StreamReader(capturePath, Encoding.UTF8))
            using (var log = new StreamWriter(logPath, false, new UTF8Encoding(false)))
            {
                result = CaptureConverter.Convert(capture, log, Path.GetFileName(capturePath));
            }

            output.WriteLine("Запись переведена: соединений " + result.Connections + ", пакетов клиента " + result.ClientPackets +
                             ", сервера " + result.ServerPackets + ", " + (result.Errors.Count == 0 ? "ошибок нет" : "ошибок " + result.Errors.Count) +
                             ". Лог: " + logPath);
            for (var i = 0; i < result.Errors.Count && i < MaxListed; i++)
            {
                output.WriteLine("  ОШИБКА: " + result.Errors[i]);
            }

            return result.Errors.Count == 0 ? BotExitCode.Success : BotExitCode.ProtocolError;
        }
    }
}
