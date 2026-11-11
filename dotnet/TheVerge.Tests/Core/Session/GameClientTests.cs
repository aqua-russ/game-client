using System.Linq;
using NUnit.Framework;
using TheVerge.Core.Session;

namespace TheVerge.Tests.Core.Session
{
    /// <summary>
    /// Ядро клиента (PLAN Стадия 1, п. 4а; AGENTS.md §2.3): Tick разбирает очередь по порядку, пока не вышел бюджет
    /// тика (ClientLimits.TickBudgetMs = 5 мс, ADR 0013 п. 3), но не меньше одного события за тик; в конце тика —
    /// одна отправка накопленного.
    /// </summary>
    public sealed class GameClientTests
    {
        [Test]
        public void TickBudget_IsFiveMilliseconds()
        {
            Assert.That(ClientLimits.TickBudgetMs, Is.EqualTo(5));
        }

        [Test]
        public void Tick_ProcessesInOrderOfArrival_ThenSendsOnce()
        {
            var clock = new FakeClock();
            var network = new FakeNetworkSession(clock);
            network.Push(1, 0);
            network.Push(2, 0);
            network.Push(3, 0);
            var client = new GameClient(network, clock);

            var processed = client.Tick(clock.NowMs);

            Assert.That(processed, Is.EqualTo(3));
            Assert.That(network.Calls, Is.EqualTo(new[] { "обработал 1", "обработал 2", "обработал 3", "отправил" }));
        }

        [Test]
        public void Tick_StopsWhenBudgetSpent_RestInNextTicks()
        {
            // Каждый пакет «стоит» 2 мс: за тик успевают три (0 → 2 → 4 → 6: после третьего бюджет 5 мс исчерпан).
            var clock = new FakeClock();
            var network = new FakeNetworkSession(clock);
            for (var i = 1; i <= 10; i++)
            {
                network.Push(i, 2);
            }

            var client = new GameClient(network, clock);

            var perTick = Enumerable.Range(0, 5).Select(_ => client.Tick(clock.NowMs)).ToArray();

            Assert.That(perTick, Is.EqualTo(new[] { 3, 3, 3, 1, 0 }));
            Assert.That(network.Calls.Where(c => c.StartsWith("обработал")),
                Is.EqualTo(Enumerable.Range(1, 10).Select(i => "обработал " + i)));
        }

        [Test]
        public void Tick_SlowPacket_StillAtLeastOnePerTick()
        {
            var clock = new FakeClock();
            var network = new FakeNetworkSession(clock);
            network.Push(1, 100);
            network.Push(2, 100);
            var client = new GameClient(network, clock);

            Assert.That(client.Tick(clock.NowMs), Is.EqualTo(1));
            Assert.That(client.Tick(clock.NowMs), Is.EqualTo(1));
        }

        [Test]
        public void Tick_BudgetCountedFromGivenTime()
        {
            // Кадр начался в 100 мс, а Tick вызван в 103: на разбор осталось 2 мс, а не 5.
            var clock = new FakeClock { NowMs = 103 };
            var network = new FakeNetworkSession(clock);
            for (var i = 1; i <= 5; i++)
            {
                network.Push(i, 1);
            }

            var client = new GameClient(network, clock);

            Assert.That(client.Tick(100), Is.EqualTo(2));
        }

        [Test]
        public void Tick_EmptyQueue_NothingProcessed_SendStillOnce()
        {
            var clock = new FakeClock();
            var network = new FakeNetworkSession(clock);
            var client = new GameClient(network, clock);

            Assert.That(client.Tick(0), Is.EqualTo(0));
            Assert.That(client.Tick(0), Is.EqualTo(0));
            Assert.That(network.Calls, Is.EqualTo(new[] { "отправил", "отправил" }));
        }

        [Test]
        public void Tick_SendIsLastStepOfTick()
        {
            var clock = new FakeClock();
            var network = new FakeNetworkSession(clock);
            for (var i = 1; i <= 4; i++)
            {
                network.Push(i, 2);
            }

            var client = new GameClient(network, clock);

            client.Tick(clock.NowMs);
            client.Tick(clock.NowMs);

            Assert.That(network.Calls, Is.EqualTo(new[]
            {
                "обработал 1", "обработал 2", "обработал 3", "отправил", "обработал 4", "отправил",
            }));
        }
    }
}
