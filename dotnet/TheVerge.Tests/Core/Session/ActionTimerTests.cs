using NUnit.Framework;
using TheVerge.Core.Session;

namespace TheVerge.Tests.Core.Session
{
    /// <summary>
    /// Общая задержка действий (ServerLimits.ActionDelayMs = 500 мс в UOR): двойной клик (IncomingEntityPackets.UseReq) и
    /// быстрый бинт (IncomingExtendedCommandPackets.BandageTarget) у сервера делят одну Mobile.NextActionTime; раньше времени
    /// сервер действие не выполняет (отвечает «You must wait…»), поэтому клиент его не шлёт.
    /// </summary>
    public sealed class ActionTimerTests
    {
        [Test]
        public void First_Allowed_ThenWaitsDelay()
        {
            var timer = new ActionTimer();

            Assert.That(timer.TryStart(1000), Is.True);
            Assert.That(timer.TryStart(1000 + ServerLimits.ActionDelayMs - 1), Is.False);
            Assert.That(timer.TryStart(1000 + ServerLimits.ActionDelayMs), Is.True);
        }

        [Test]
        public void Refused_DoesNotRestartDelay()
        {
            var timer = new ActionTimer();
            timer.TryStart(1000);

            timer.TryStart(1200);

            Assert.That(timer.TryStart(1500), Is.True);
        }
    }
}
