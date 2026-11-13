using System.Collections.Generic;
using System.IO;
using TheVerge.Network.Diagnostics;

namespace TheVerge.Bot.Replay
{
    /// <summary>
    /// Лог пакетов (Network/Diagnostics, формат v1), разложенный по соединениям: каждое начинается строкой CONNECTED.
    /// Пакеты, которые не ушли (пометка unsent), выброшены: сервер их не видел.
    /// </summary>
    public sealed class ReplayScript
    {
        private ReplayScript(List<List<PacketLogEntry>> connections)
        {
            Connections = connections;
        }

        public IReadOnlyList<IReadOnlyList<PacketLogEntry>> Connections { get; }

        public static ReplayScript Read(TextReader input)
        {
            var connections = new List<List<PacketLogEntry>>();
            var reader = new PacketLogReader(input);
            while (reader.TryRead(out var entry))
            {
                if (entry.Kind == PacketLogKind.Connected)
                {
                    connections.Add(new List<PacketLogEntry>());
                }

                if (connections.Count > 0 && (entry.Kind != PacketLogKind.Packet || entry.IsSent))
                {
                    connections[connections.Count - 1].Add(entry);
                }
            }

            return new ReplayScript(connections);
        }
    }
}
