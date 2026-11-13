using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.Chat;
using TheVerge.Core.Ports;
using TheVerge.Core.Session;
using TheVerge.Tests.Core.Session;

namespace TheVerge.Tests.Core.Chat
{
    /// <summary>
    /// Речь игрока (PLAN Стадия 2, п. 10). Сервер обрезает пробелы по краям и молча выбрасывает пустой текст и длиннее 128
    /// букв (IncomingMessagePackets.cs, UnicodeSpeech: text.Trim(), Length is &lt;= 0 or &gt; 128); речь игрока чаще 25 мс
    /// отбрасывается (throttles.json, docs/TARGET.md §3) — клиент ставит такую фразу в очередь.
    /// </summary>
    public sealed class SpeechCommandsTests
    {
        private FakeClock _clock = null!;
        private RecordingSpeechSender _sender = null!;
        private SpeechCommands _speech = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock { NowMs = 1000 };
            _sender = new RecordingSpeechSender();
            _speech = new SpeechCommands(_sender, _clock);
        }

        [Test]
        public void Say_SentTrimmedWithClientHue()
        {
            Assert.That(_speech.Say("  [where  "), Is.EqualTo(SpeechResult.Sent));

            Assert.That(_sender.Sent, Is.EqualTo(new[] { "[where" }));
            Assert.That(_sender.Hues, Is.EqualTo(new[] { ClientLimits.SpeechHue }));
        }

        [TestCase("")]
        [TestCase("   ")]
        public void Say_Empty_NotSent(string text)
        {
            Assert.That(_speech.Say(text), Is.EqualTo(SpeechResult.Empty));
            Assert.That(_sender.Sent, Is.Empty);
        }

        [Test]
        public void Say_128Letters_Sent_129_NotSent()
        {
            Assert.That(_speech.Say(new string('я', ServerLimits.SpeechMaxChars)), Is.EqualTo(SpeechResult.Sent));
            _clock.NowMs += ServerLimits.SpeechThrottleMs;
            Assert.That(_speech.Say(new string('я', ServerLimits.SpeechMaxChars + 1)), Is.EqualTo(SpeechResult.TooLong));
            Assert.That(_sender.Sent.Count, Is.EqualTo(1));
        }

        [Test]
        public void Say_TooSoon_QueuedThenSentInTick()
        {
            _speech.Say("раз");
            _clock.NowMs += ServerLimits.SpeechThrottleMs - 1;

            Assert.That(_speech.Say("два"), Is.EqualTo(SpeechResult.Queued));
            Assert.That(_speech.Pending, Is.EqualTo(1));
            _speech.Update(_clock.NowMs);
            Assert.That(_sender.Sent, Is.EqualTo(new[] { "раз" }));

            _clock.NowMs += 1;
            _speech.Update(_clock.NowMs);
            Assert.That(_sender.Sent, Is.EqualTo(new[] { "раз", "два" }));
            Assert.That(_speech.Pending, Is.EqualTo(0));
        }

        [Test]
        public void Say_QueuedKeepsOrder_OnePerInterval()
        {
            _speech.Say("1");
            _speech.Say("2");
            _speech.Say("3");

            _clock.NowMs += ServerLimits.SpeechThrottleMs;
            _speech.Update(_clock.NowMs);
            _speech.Update(_clock.NowMs);

            Assert.That(_sender.Sent, Is.EqualTo(new[] { "1", "2" }));
        }

        [Test]
        public void Say_QueueFull_Refused()
        {
            _speech.Say("first");
            for (var i = 0; i < SpeechCommands.QueueLimit; i++)
            {
                Assert.That(_speech.Say("q" + i), Is.EqualTo(SpeechResult.Queued));
            }

            Assert.That(_speech.Say("lost"), Is.EqualTo(SpeechResult.QueueFull));
        }

        private sealed class RecordingSpeechSender : ISpeechSender
        {
            public List<string> Sent { get; } = new List<string>();

            public List<ushort> Hues { get; } = new List<ushort>();

            public void SendSpeech(string text, ushort hue)
            {
                Sent.Add(text);
                Hues.Add(hue);
            }
        }
    }
}
