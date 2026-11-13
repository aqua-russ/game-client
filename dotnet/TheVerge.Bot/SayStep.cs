using TheVerge.Core.Chat;

namespace TheVerge.Bot
{
    /// <summary>say:&lt;текст&gt; — сказать; GM-команды — тоже речь («[go 1500 1600»). Очередь полна — попробовать в следующем тике.</summary>
    internal sealed class SayStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly string _text;

        public SayStep(BotWorld world, string text)
        {
            _world = world;
            _text = text;
        }

        public string TimeoutProblem => "очередь речи полна: фраза не ушла.";

        public string Failure => string.Empty;

        public bool Poll()
        {
            var result = _world.Speech.Say(_text);
            return result == SpeechResult.Sent || result == SpeechResult.Queued;
        }

        public void Finish()
        {
        }
    }
}
