namespace TheVerge.Core.Ports
{
    /// <summary>Отправка речи игрока (ADR 0008 п. 1). Реализация — Network/Packets/Chat/SpeechSender.</summary>
    public interface ISpeechSender
    {
        /// <summary>Сказать текст (уже проверенный: 1–128 букв) этим цветом; язык — ClientLimits.Language.</summary>
        void SendSpeech(string text, ushort hue);
    }
}
