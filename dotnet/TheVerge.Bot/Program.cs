using System;
using TheVerge.Core.Session;
using TheVerge.Data;
using TheVerge.Network;

namespace TheVerge.Bot
{
    /// <summary>
    /// Headless-клиент без Unity. Пока только подтверждает, что Core, Network и Data загружаются и работают
    /// вне Unity. Вход на сервер и сценарии появятся на Стадии 1 (docs/PLAN.md §3).
    /// </summary>
    public static class Program
    {
        public static int Main()
        {
            Console.WriteLine("TheVerge.Bot: сборки загружены:");
            PrintAssembly(typeof(ServerLimits));
            PrintAssembly(typeof(HuffmanCodec));
            PrintAssembly(typeof(DataAssembly));
            Console.WriteLine("TheVerge.Bot: ОК (вход на сервер появится на Стадии 1)");
            return 0;
        }

        private static void PrintAssembly(Type marker)
        {
            var name = marker.Assembly.GetName();
            Console.WriteLine("  " + name.Name + " " + name.Version);
        }
    }
}
