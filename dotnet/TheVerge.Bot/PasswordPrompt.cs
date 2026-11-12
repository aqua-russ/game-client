using System;
using System.Text;

namespace TheVerge.Bot
{
    /// <summary>Скрытый ввод пароля в консоли: буквы не показываются и не попадают в историю Терминала.</summary>
    public static class PasswordPrompt
    {
        public static string? Read()
        {
            Console.Write("Пароль (не отображается): ");
            var password = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                {
                    break;
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    password.Length = Math.Max(0, password.Length - 1);
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    password.Append(key.KeyChar);
                }
            }

            Console.WriteLine();
            return password.ToString();
        }
    }
}
