namespace TheVerge.Core.Ports
{
    /// <summary>
    /// Временный порт для старого окна статуса сети (NetworkStatusHudPresenter): даёт ему счётчики
    /// без ссылки Presentation → Network (ADR 0008). Уходит вместе с этим окном на Стадии 1 (docs/TECH-DEBT.md).
    /// </summary>
    public interface INetworkDiagnostics
    {
        bool IsConnected { get; }
        bool IsLiveServer { get; }
        int ConnectedPort { get; }
        int SentPacketsCount { get; }
        int ReceivedPacketsCount { get; }
        int ReceivedBytesCount { get; }
        byte LastSequenceKey { get; }
        string AuthStageDescription { get; }

        /// <summary>Отладочная кнопка окна статуса: имитирует отказ шага (0x21).</summary>
        void SimulateMoveReject();
    }
}
