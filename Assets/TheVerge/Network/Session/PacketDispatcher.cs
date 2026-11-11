using System;
using System.Collections.Generic;
using TheVerge.Core.Ports;
using TheVerge.Network.Framing;
using TheVerge.Network.IO;

namespace TheVerge.Network.Session
{
    /// <summary>
    /// Разбор входящих пакетов по номеру и подкоманде (AGENTS.md §2.3, шаг 2; ADR 0013 п. 7). Обработчики регистрирует
    /// Bootstrap; номер и подкоманда сверяются с матрицей пакетов сразу при регистрации. Пакет без обработчика —
    /// лог один раз на номер и пропуск; пакет короче, чем ждёт обработчик, — лог с дампом и пропуск (AGENTS.md §3.5).
    /// Остальные исключения обработчика — ошибка кода клиента: они не глотаются.
    /// </summary>
    public sealed class PacketDispatcher
    {
        private const PacketDirection Incoming = PacketDirection.ServerToClient;

        private readonly ILog _log;
        private readonly PacketHandler?[] _handlers = new PacketHandler?[byte.MaxValue + 1];
        private readonly Dictionary<int, PacketHandler> _subCommandHandlers = new Dictionary<int, PacketHandler>();
        private readonly HashSet<int> _reportedUnhandled = new HashSet<int>();

        public PacketDispatcher(ILog log)
        {
            _log = log;
        }

        public void Register(byte id, PacketHandler handler)
        {
            RequireKnown(id, PacketTable.Get(Incoming, id), "0x" + id.ToString("X2"));
            if (PacketTable.GetSubCommandLayout(Incoming, id).HasSubCommand)
            {
                throw new ArgumentException("У пакета 0x" + id.ToString("X2") + " есть подкоманды: обработчик регистрируется на подкоманду.");
            }

            if (_handlers[id] != null)
            {
                throw new ArgumentException("Обработчик пакета 0x" + id.ToString("X2") + " уже зарегистрирован.");
            }

            _handlers[id] = handler;
        }

        public void Register(byte id, ushort subCommand, PacketHandler handler)
        {
            var label = "0x" + id.ToString("X2") + " 0x" + subCommand.ToString("X2");
            if (!PacketTable.GetSubCommandLayout(Incoming, id).HasSubCommand)
            {
                throw new ArgumentException("У пакета 0x" + id.ToString("X2") + " нет подкоманд.");
            }

            RequireKnown(id, PacketTable.Get(Incoming, id, subCommand), label);
            if (!_subCommandHandlers.ContainsKey(Key(id, subCommand)))
            {
                _subCommandHandlers.Add(Key(id, subCommand), handler);
                return;
            }

            throw new ArgumentException("Обработчик пакета " + label + " уже зарегистрирован.");
        }

        /// <summary>Разобрать целый пакет от нарезки (номер и длина уже проверены по матрице).</summary>
        public DispatchResult Dispatch(ReadOnlySpan<byte> packet)
        {
            var id = packet[0];
            var info = PacketTable.Get(Incoming, id);
            var layout = PacketTable.GetSubCommandLayout(Incoming, id);
            var payloadStart = info.IsVariable ? PacketInfo.VariableHeaderLength : PacketInfo.IdLength;
            var handler = _handlers[id];
            ushort subCommand = 0;
            if (layout.HasSubCommand)
            {
                subCommand = layout.Read(packet);
                payloadStart = layout.MinPacketLength;
                info = PacketTable.Get(Incoming, id, subCommand);
                _subCommandHandlers.TryGetValue(Key(id, subCommand), out handler);
            }

            if (handler == null)
            {
                ReportUnhandled(id, layout.HasSubCommand, subCommand, info.Name);
                return DispatchResult.NoHandler;
            }

            return Invoke(handler, packet, payloadStart, Label(id, layout.HasSubCommand, subCommand, info.Name));
        }

        private DispatchResult Invoke(PacketHandler handler, ReadOnlySpan<byte> packet, int payloadStart, string label)
        {
            var reader = new PacketReader(packet.Slice(payloadStart));
            try
            {
                handler(ref reader);
                return DispatchResult.Handled;
            }
            catch (PacketOutOfRangeException e)
            {
                _log.Warning("Пакет " + label + " пропущен: " + e.Message + " Дамп (" + packet.Length + " байт): " +
                             PacketDump.ToHex(packet));
                return DispatchResult.ReadError;
            }
        }

        private void ReportUnhandled(byte id, bool hasSubCommand, ushort subCommand, string name)
        {
            if (_reportedUnhandled.Add(hasSubCommand ? Key(id, subCommand) : id))
            {
                _log.Info("Пакет " + Label(id, hasSubCommand, subCommand, name) +
                          " пока не обрабатывается: пропущен (дальше такие не пишутся в лог).");
            }
        }

        private static void RequireKnown(byte id, PacketInfo info, string label)
        {
            if (!info.IsKnown)
            {
                throw new ArgumentException("Сервер не шлёт пакет " + label + ": его нет в docs/protocol/packets.csv.");
            }
        }

        private static string Label(byte id, bool hasSubCommand, ushort subCommand, string name) =>
            "0x" + id.ToString("X2") + (hasSubCommand ? " 0x" + subCommand.ToString("X2") : string.Empty) + " " + name;

        // Номера от 0 до 0xFF: ключ «номер и подкоманда» отличается от ключа одного номера.
        private static int Key(byte id, ushort subCommand) => ((id + 1) << 16) | subCommand;
    }
}
