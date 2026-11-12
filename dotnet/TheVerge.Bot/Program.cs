using System;
using System.Text;
using System.Threading;

namespace TheVerge.Bot
{
    /// <summary>
    /// Точка входа бота: консоль, Ctrl+C и поиск корня клиента. Всё остальное — <see cref="BotApp"/>.
    /// Справка — <see cref="BotUsage"/> (tools/bot/run.sh --help).
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            using (var interrupt = new CancellationTokenSource())
            {
                // Ctrl+C не убивает процесс сразу: бот закрывает соединение, дописывает лог и выходит с кодом 130.
                ConsoleCancelEventHandler onCancel = (sender, e) =>
                {
                    e.Cancel = true;
                    interrupt.Cancel();
                };
                Console.CancelKeyPress += onCancel;
                try
                {
                    var clientRoot = BotPaths.FindClientRoot(AppContext.BaseDirectory);
                    var prompt = Console.IsInputRedirected ? null : new Func<string?>(PasswordPrompt.Read);
                    return new BotApp(Console.Out, clientRoot, prompt).Run(args, interrupt.Token);
                }
                finally
                {
                    Console.CancelKeyPress -= onCancel;
                }
            }
        }
    }
}
