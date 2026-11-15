using System;
using TheVerge.Core.Session;

namespace TheVerge.Core.Diagnostics
{
    /// <summary>
    /// Настройки режима «симуляция пинга» (PLAN Стадия 3, п. 9; ADR 0020 п. 9): задержка и разброс отдельно для пакетов от
    /// сервера (<see cref="Incoming"/>) и к серверу (<see cref="Outgoing"/>). По умолчанию выключено; значения по умолчанию
    /// и пределы — <see cref="ClientLimits"/> (TARGET.md §3.1), вне пределов значения обрезаются. Протокол не меняется: пакеты
    /// задерживает соединение-обёртка Network/Diagnostics/LaggedConnection, Unity только включает режим и меняет значения.
    /// </summary>
    public sealed class LagSimulation
    {
        private int _incomingDelayMs = ClientLimits.LagSimulationDefaultDelayMs;
        private int _incomingJitterMs = ClientLimits.LagSimulationDefaultJitterMs;
        private int _outgoingDelayMs = ClientLimits.LagSimulationDefaultDelayMs;
        private int _outgoingJitterMs = ClientLimits.LagSimulationDefaultJitterMs;

        public bool Enabled { get; set; }

        public int IncomingDelayMs
        {
            get => _incomingDelayMs;
            set => _incomingDelayMs = ClampDelay(value);
        }

        public int IncomingJitterMs
        {
            get => _incomingJitterMs;
            set => _incomingJitterMs = ClampJitter(value);
        }

        public int OutgoingDelayMs
        {
            get => _outgoingDelayMs;
            set => _outgoingDelayMs = ClampDelay(value);
        }

        public int OutgoingJitterMs
        {
            get => _outgoingJitterMs;
            set => _outgoingJitterMs = ClampJitter(value);
        }

        /// <summary>Задержка пакетов от сервера; выключено — <see cref="LagSettings.None"/>.</summary>
        public LagSettings Incoming => Enabled ? new LagSettings(_incomingDelayMs, _incomingJitterMs) : LagSettings.None;

        /// <summary>Задержка пакетов к серверу; выключено — <see cref="LagSettings.None"/>.</summary>
        public LagSettings Outgoing => Enabled ? new LagSettings(_outgoingDelayMs, _outgoingJitterMs) : LagSettings.None;

        private static int ClampDelay(int value) => Math.Min(Math.Max(value, 0), ClientLimits.LagSimulationMaxDelayMs);

        private static int ClampJitter(int value) => Math.Min(Math.Max(value, 0), ClientLimits.LagSimulationMaxJitterMs);
    }
}
