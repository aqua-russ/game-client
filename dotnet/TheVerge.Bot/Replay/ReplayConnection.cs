using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Framing;
using TheVerge.Network.Transport;

namespace TheVerge.Bot.Replay
{
    /// <summary>
    /// Одно соединение фейкового сервера: проигрывает свою часть лога. Пакеты сервера до первого пакета клиента приходят
    /// сразу после подключения; дальше на каждый пакет клиента, которого ждёт лог (сверяются номер и подкоманда —
    /// содержимое вроде seed, ключа и времени меняется от запуска к запуску), — следующие пакеты сервера из лога. Не тот
    /// пакет — расхождение (<see cref="ReplayServer.Mismatches"/>) и разрыв, а не зависание. Seed игрового соединения
    /// (4 сырых байта перед 0x91) в логе не пишется и пропускается.
    /// </summary>
    internal sealed class ReplayConnection : IConnection
    {
        private const int GameSeedLength = 4;

        private readonly IReadOnlyList<PacketLogEntry> _entries;
        private readonly ReplayServer _server;
        private readonly ConcurrentQueue<InboundEvent> _inbound = new ConcurrentQueue<InboundEvent>();
        private int _position;

        public ReplayConnection(IReadOnlyList<PacketLogEntry> entries, ReplayServer server)
        {
            _entries = entries;
            _server = server;
        }

        public ConnectionState State { get; private set; } = ConnectionState.Idle;

        /// <summary>Все пакеты клиента из этой части лога пришли.</summary>
        public bool IsComplete => NextClientPacket() < 0;

        public void Connect(string host, int port, CancellationToken cancellation)
        {
            cancellation.Register(() => Close(ConnectionCloseReason.Cancelled, string.Empty));
            State = ConnectionState.Connected;
            if (_position < _entries.Count && _entries[_position].Kind == PacketLogKind.Connected)
            {
                _position++;
                _inbound.Enqueue(InboundEvent.Connected());
            }

            PlayServerPackets();
        }

        public void EnableCompression()
        {
            // Пакеты лога уже распакованы: проигрываются целыми, без Huffman.
        }

        public bool Send(ReadOnlySpan<byte> data)
        {
            if (State != ConnectionState.Connected)
            {
                return false;
            }

            var offset = SkipGameSeed(data);
            while (offset < data.Length && State == ConnectionState.Connected)
            {
                var length = PacketLength(data.Slice(offset));
                Expect(data.Slice(offset, length));
                offset += length;
            }

            return true;
        }

        public bool TryReceive(out InboundEvent item) => _inbound.TryDequeue(out item);

        public void Dispose() => State = ConnectionState.Closed;

        private void Expect(ReadOnlySpan<byte> packet)
        {
            var expected = NextClientPacket();
            if (expected < 0 || !SamePacket(_entries[expected].Packet, packet))
            {
                var wanted = expected < 0 ? "конец записи" : Label(_entries[expected].Packet);
                _server.Report("ждали " + wanted + ", пришёл " + Label(packet.ToArray()));
                Close(ConnectionCloseReason.SocketError, "фейковый сервер: клиент разошёлся с записью");
                return;
            }

            _position = expected + 1;
            PlayServerPackets();
        }

        // Пакеты сервера подряд — до следующего пакета клиента; закрытие сервером тоже проигрывается.
        private void PlayServerPackets()
        {
            while (_position < _entries.Count)
            {
                var entry = _entries[_position];
                if (entry.Kind == PacketLogKind.Packet && entry.Direction == PacketDirection.ClientToServer)
                {
                    return;
                }

                _position++;
                if (entry.Kind == PacketLogKind.Packet)
                {
                    _inbound.Enqueue(InboundEvent.FromPacket(Rent(entry.Packet), entry.Packet.Length, ArrayPool<byte>.Shared));
                }
                else if (entry.Kind == PacketLogKind.Closed && entry.CloseReason != ConnectionCloseReason.Cancelled)
                {
                    Close(entry.CloseReason, entry.Detail);
                }
            }
        }

        private int NextClientPacket()
        {
            for (var i = _position; i < _entries.Count; i++)
            {
                if (_entries[i].Kind == PacketLogKind.Packet && _entries[i].Direction == PacketDirection.ClientToServer)
                {
                    return i;
                }
            }

            return -1;
        }

        private int SkipGameSeed(ReadOnlySpan<byte> data)
        {
            var expected = NextClientPacket();
            var awaitsGameLogin = expected >= 0 && _entries[expected].Packet[0] == GameLoginId;
            return awaitsGameLogin && data.Length > GameSeedLength && data[0] != GameLoginId && data[GameSeedLength] == GameLoginId
                ? GameSeedLength
                : 0;
        }

        private void Close(ConnectionCloseReason reason, string detail)
        {
            if (State == ConnectionState.Closed)
            {
                return;
            }

            State = ConnectionState.Closed;
            _inbound.Enqueue(InboundEvent.Closed(reason, detail));
        }

        private const byte GameLoginId = 0x91;

        private static int PacketLength(ReadOnlySpan<byte> data)
        {
            var info = PacketTable.Get(PacketDirection.ClientToServer, data[0]);
            var length = info.IsVariable ? BinaryPrimitives.ReadUInt16BigEndian(data.Slice(PacketInfo.LengthOffset)) : info.FixedLength;
            return Math.Min(Math.Max(length, 1), data.Length);
        }

        private static bool SamePacket(byte[] expected, ReadOnlySpan<byte> actual) =>
            expected[0] == actual[0] && SubCommand(expected) == SubCommand(actual);

        private static int SubCommand(ReadOnlySpan<byte> packet)
        {
            var layout = PacketTable.GetSubCommandLayout(PacketDirection.ClientToServer, packet[0]);
            return layout.HasSubCommand && packet.Length >= layout.MinPacketLength ? layout.Read(packet) : -1;
        }

        private static string Label(byte[] packet)
        {
            var sub = SubCommand(packet);
            return "0x" + packet[0].ToString("X2") + (sub >= 0 ? " 0x" + sub.ToString("X2") : string.Empty) + " " +
                   PacketTable.Get(PacketDirection.ClientToServer, packet[0]).Name;
        }

        private static byte[] Rent(byte[] packet)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(packet.Length);
            Buffer.BlockCopy(packet, 0, buffer, 0, packet.Length);
            return buffer;
        }
    }
}
