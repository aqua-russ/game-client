using NUnit.Framework;
using TheVerge.Core.Session;
using TheVerge.Core.Session.Screens;

namespace TheVerge.Tests.Core.Session.Screens
{
    /// <summary>
    /// Экран входа (PLAN Стадия 1, п. 16): адрес «хост:порт», логин и пароль. Кнопка «Войти» активна, только когда всё
    /// можно отправить серверу без искажений: логин и пароль сервер читает как Latin-1 по 30 байт
    /// (IncomingAccountPackets.cs, AccountLogin: ReadLatin1Safe(30)). Пароль нигде не показывается.
    /// </summary>
    public sealed class LoginFormTests
    {
        [Test]
        public void Default_AddressOfLocalStand_SubmitDisabledUntilLoginAndPassword()
        {
            var form = new LoginForm();

            Assert.That(form.Address, Is.EqualTo("127.0.0.1:2593"));
            Assert.That(form.CanSubmit, Is.False);
            Assert.That(form.Problem, Is.EqualTo("Введите логин."));

            form.Username = "player";
            Assert.That(form.Problem, Is.EqualTo("Введите пароль."));

            form.Password = "secret";
            Assert.That(form.CanSubmit, Is.True);
            Assert.That(form.Problem, Is.Empty);
        }

        [Test]
        public void Valid_GivesHostPortAndCredentials()
        {
            var form = Filled();
            form.Address = "  192.168.1.20:2600 ";

            Assert.That(form.ServerAddress.Host, Is.EqualTo("192.168.1.20"));
            Assert.That(form.ServerAddress.Port, Is.EqualTo(2600));
            Assert.That(form.Credentials.Username, Is.EqualTo("player"));
            Assert.That(form.Credentials.Password, Is.EqualTo("secret"));
        }

        [TestCase("")]
        [TestCase("127.0.0.1")]
        [TestCase("127.0.0.1:")]
        [TestCase(":2593")]
        [TestCase("127.0.0.1:порт")]
        [TestCase("127.0.0.1:0")]
        [TestCase("127.0.0.1:65536")]
        [TestCase("127.0 .0.1:2593")]
        public void BadAddress_HintAndSubmitDisabled(string address)
        {
            var form = Filled();
            form.Address = address;

            Assert.That(form.CanSubmit, Is.False);
            Assert.That(form.Problem, Does.StartWith("Адрес сервера").And.Contain("127.0.0.1:2593"));
        }

        [Test]
        public void LoginTooLongOrNotLatin1_HintAndSubmitDisabled()
        {
            var form = Filled();
            form.Username = new string('a', ServerLimits.CredentialMaxChars + 1);
            Assert.That(form.CanSubmit, Is.False);
            Assert.That(form.Problem, Does.StartWith("Логин").And.Contain("30"));

            form.Username = "игрок";
            Assert.That(form.Problem, Does.StartWith("Логин").And.Contain("Latin-1"));

            form.Username = new string('a', ServerLimits.CredentialMaxChars);
            Assert.That(form.CanSubmit, Is.True);
        }

        [Test]
        public void BadPassword_HintNeverShowsPassword()
        {
            var form = Filled();
            form.Password = "пароль-секрет";

            Assert.That(form.CanSubmit, Is.False);
            Assert.That(form.Problem, Does.StartWith("Пароль").And.Not.Contain("секрет"));
        }

        [Test]
        public void Change_GrowsRevision_SameValueDoesNot()
        {
            var form = new LoginForm();
            var before = form.Revision;

            form.Username = "player";
            Assert.That(form.Revision, Is.GreaterThan(before));

            var after = form.Revision;
            form.Username = "player";
            Assert.That(form.Revision, Is.EqualTo(after));
        }

        [Test]
        public void Credentials_ProblemMatchesConstructor()
        {
            Assert.That(LoginCredentials.Problem("player"), Is.Empty);
            Assert.That(LoginCredentials.Problem(string.Empty), Does.Contain("от 1 до 30"));
            Assert.That(LoginCredentials.Problem("игрок"), Does.Contain("Latin-1"));
        }

        private static LoginForm Filled() => new LoginForm { Username = "player", Password = "secret" };
    }
}
