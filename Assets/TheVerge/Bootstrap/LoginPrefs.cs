using TheVerge.Core.Session.Screens;
using UnityEngine;

namespace TheVerge.Bootstrap
{
    /// <summary>
    /// Последний адрес сервера и логин — в настройках Unity на этом компьютере (PlayerPrefs), чтобы не вводить их каждый
    /// раз. Пароль не сохраняется никогда.
    /// </summary>
    public static class LoginPrefs
    {
        private const string AddressKey = "TheVerge.Login.Address";
        private const string UsernameKey = "TheVerge.Login.Username";

        public static void Load(LoginForm form)
        {
            form.Address = PlayerPrefs.GetString(AddressKey, form.Address);
            form.Username = PlayerPrefs.GetString(UsernameKey, form.Username);
        }

        public static void Save(LoginForm form)
        {
            PlayerPrefs.SetString(AddressKey, form.Address.Trim());
            PlayerPrefs.SetString(UsernameKey, form.Username);
            PlayerPrefs.Save();
        }
    }
}
