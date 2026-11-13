using NUnit.Framework;
using TheVerge.Core.Session;
using TheVerge.Core.Session.Screens;

namespace TheVerge.Tests.Core.Session.Screens
{
    /// <summary>
    /// Причины отказов и ошибок входа — словами для человека (PLAN Стадия 1, п. 16). Коды — сервера:
    /// ALRReason и DeleteResultType (Projects/Server/Network/Packets/OutgoingAccountPackets.cs), PMMessage (там же),
    /// отказ в удалении — UOContent/Accounting/AccountHandler.cs, DeleteRequest.
    /// </summary>
    public sealed class LoginErrorTextTests
    {
        [TestCase(LoginRejectReason.Invalid, "нет такого аккаунта")]
        [TestCase(LoginRejectReason.InUse, "аккаунт уже в игре")]
        [TestCase(LoginRejectReason.Blocked, "аккаунт заблокирован")]
        [TestCase(LoginRejectReason.BadPassword, "неверный пароль")]
        [TestCase(LoginRejectReason.Idle, "слишком долго молчал")]
        [TestCase(LoginRejectReason.BadCommunication, "maxAccountsPerIP")]
        public void LoginDenied_ReasonInWords(LoginRejectReason reason, string words)
        {
            var text = LoginErrorText.Describe(new LoginError(LoginErrorKind.LoginDenied, (int)reason, reason.ToString()), -1);

            Assert.That(text, Does.StartWith("Сервер отказал во входе: ").And.Contain(words));
        }

        [Test]
        public void LoginDenied_UnknownCode_ShowsCode()
        {
            Assert.That(LoginErrorText.Reject((LoginRejectReason)77), Is.EqualTo("код 77"));
        }

        [Test]
        public void ConnectFailedAndTimeout_SayWhatToCheck()
        {
            var failed = LoginErrorText.Describe(new LoginError(LoginErrorKind.ConnectFailed, (int)DisconnectReason.ConnectFailed, "refused"), -1);
            var slow = LoginErrorText.Describe(new LoginError(LoginErrorKind.ConnectFailed, (int)DisconnectReason.ConnectTimeout, string.Empty), -1);
            var timeout = LoginErrorText.Describe(new LoginError(LoginErrorKind.Timeout, 2, "ждали список"), -1);

            Assert.That(failed, Does.StartWith("Не удалось подключиться к серверу").And.Contain("запущен ли сервер"));
            Assert.That(slow, Does.Contain("не ответил на подключение"));
            Assert.That(timeout, Does.StartWith("Сервер не ответил вовремя"));
        }

        [Test]
        public void CharacterRejected_InWorld_InWords()
        {
            var text = LoginErrorText.Describe(
                new LoginError(LoginErrorKind.CharacterRejected, (int)CharacterRejectReason.CharInWorld, string.Empty), -1);

            Assert.That(text, Does.Contain("другой персонаж этого аккаунта уже в мире"));
        }

        [Test]
        public void ServerClosedSoonAfterEnteringWorld_AddsHint()
        {
            var error = new LoginError(LoginErrorKind.Disconnected, (int)DisconnectReason.ServerClosed, string.Empty);

            Assert.That(LoginErrorText.Describe(error, 3000), Does.StartWith("Сервер закрыл соединение").And.Contain("clientVerification"));
            Assert.That(LoginErrorText.Describe(error, 600000), Does.Not.Contain("clientVerification"));
        }

        [Test]
        public void NetworkErrorAndProtocol_InWords()
        {
            var network = LoginErrorText.Describe(new LoginError(LoginErrorKind.Disconnected, (int)DisconnectReason.NetworkError, "reset"), -1);
            var protocol = LoginErrorText.Describe(new LoginError(LoginErrorKind.ProtocolError, 5, "длина"), -1);

            Assert.That(network, Does.StartWith("Соединение потеряно"));
            Assert.That(protocol, Does.StartWith("Сервер прислал данные, которые клиент не понимает"));
        }

        [TestCase(CharacterDeleteResult.PasswordInvalid, "неверный пароль")]
        [TestCase(CharacterDeleteResult.CharNotExist, "такого персонажа нет")]
        [TestCase(CharacterDeleteResult.CharBeingPlayed, "персонаж сейчас в игре")]
        [TestCase(CharacterDeleteResult.CharTooYoung, "слишком молод")]
        [TestCase(CharacterDeleteResult.CharQueued, "очеред")]
        [TestCase(CharacterDeleteResult.BadRequest, "тюрьм")]
        public void DeleteRejected_ReasonInWords(CharacterDeleteResult result, string words)
        {
            Assert.That(LoginErrorText.DeleteRejected(result), Does.Contain(words));
        }
    }
}
