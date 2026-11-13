using System.Collections.Generic;
using TheVerge.Core.Ports;
using TheVerge.Core.Session;

namespace TheVerge.Core.Chat
{
    /// <summary>
    /// Речь игрока и текстовые GM-команды (PLAN Стадия 2, п. 10; приём речи и журнал — Стадия 4). Сервер обрезает пробелы
    /// и молча выбрасывает пустой текст и длиннее 128 букв (IncomingMessagePackets.cs, UnicodeSpeech), а речь игрока
    /// чаще <see cref="ServerLimits.SpeechThrottleMs"/> отбрасывает (throttles.json) — поэтому клиент такое не шлёт, а
    /// частые фразы ставит в очередь и отправляет по одной в тике (<see cref="Update"/>).
    /// </summary>
    public sealed class SpeechCommands : ITickHandler
    {
        /// <summary>Сколько фраз может ждать отправки.</summary>
        public const int QueueLimit = 16;

        private readonly ISpeechSender _sender;
        private readonly IClock _clock;
        private readonly Queue<string> _queue = new Queue<string>(QueueLimit);
        private bool _spoke;
        private long _lastMs;

        public SpeechCommands(ISpeechSender sender, IClock clock)
        {
            _sender = sender;
            _clock = clock;
        }

        /// <summary>Фраз в очереди.</summary>
        public int Pending => _queue.Count;

        public SpeechResult Say(string text)
        {
            var trimmed = text.Trim();
            if (trimmed.Length == 0)
            {
                return SpeechResult.Empty;
            }

            if (trimmed.Length > ServerLimits.SpeechMaxChars)
            {
                return SpeechResult.TooLong;
            }

            var now = _clock.NowMs;
            if (_queue.Count == 0 && CanSpeak(now))
            {
                Speak(trimmed, now);
                return SpeechResult.Sent;
            }

            if (_queue.Count >= QueueLimit)
            {
                return SpeechResult.QueueFull;
            }

            _queue.Enqueue(trimmed);
            return SpeechResult.Queued;
        }

        /// <summary>Отправить следующую фразу из очереди, если пора.</summary>
        public void Update(long now)
        {
            if (_queue.Count > 0 && CanSpeak(now))
            {
                Speak(_queue.Dequeue(), now);
            }
        }

        private bool CanSpeak(long now) => !_spoke || now - _lastMs >= ServerLimits.SpeechThrottleMs;

        private void Speak(string text, long now)
        {
            _spoke = true;
            _lastMs = now;
            _sender.SendSpeech(text, ClientLimits.SpeechHue);
        }
    }
}
