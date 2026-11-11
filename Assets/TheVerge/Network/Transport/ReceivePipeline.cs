using System;
using System.Buffers;
using System.Threading;
using TheVerge.Network.Compression;
using TheVerge.Network.Framing;

namespace TheVerge.Network.Transport
{
    /// <summary>
    /// Что делает фоновый поток с прочитанными байтами (AGENTS.md §2.3, шаг 1): распаковывает Huffman, если сжатие
    /// включено, режет на целые пакеты и кладёт каждый в очередь в своём буфере из пула. При ошибке нарезки кладёт
    /// одно событие <see cref="InboundKind.FramingFailed"/> и больше ничего не принимает.
    /// </summary>
    public sealed class ReceivePipeline
    {
        private const int DecodeBufferSize = 8 * 1024;

        private readonly InboundQueue _queue;
        private readonly ArrayPool<byte> _pool;
        private readonly PacketFramer _framer = new PacketFramer(PacketDirection.ServerToClient);
        private readonly HuffmanDecoder _decoder = new HuffmanDecoder();
        private readonly byte[] _decoded = new byte[DecodeBufferSize];
        private int _compressionEnabled;

        public ReceivePipeline(InboundQueue queue, ArrayPool<byte> pool)
        {
            _queue = queue;
            _pool = pool;
        }

        public bool IsFailed => _framer.IsFailed;

        /// <summary>
        /// Включить распаковку. Вызывается главным потоком до отправки 0x91: сервер начинает сжимать сразу после него
        /// (IncomingAccountPackets.cs, GameLogin), поэтому ни один сжатый байт не придёт раньше.
        /// </summary>
        public void EnableCompression() => Interlocked.Exchange(ref _compressionEnabled, 1);

        public void Receive(ReadOnlySpan<byte> data)
        {
            if (Volatile.Read(ref _compressionEnabled) == 0)
            {
                Frame(data);
                return;
            }

            while (!data.IsEmpty && !IsFailed)
            {
                var written = _decoder.Decode(data, _decoded, out var consumed);
                data = data.Slice(consumed);
                Frame(new ReadOnlySpan<byte>(_decoded, 0, written));
            }
        }

        private void Frame(ReadOnlySpan<byte> data)
        {
            while (!data.IsEmpty && !IsFailed)
            {
                data = data.Slice(_framer.Append(data));
                Drain();
            }
        }

        private void Drain()
        {
            FrameStatus status;
            while ((status = _framer.Next(out var packet)) == FrameStatus.Packet)
            {
                var buffer = _pool.Rent(packet.Length);
                packet.CopyTo(buffer);
                _queue.Enqueue(InboundEvent.FromPacket(buffer, packet.Length, _pool));
            }

            if (status == FrameStatus.Error)
            {
                _queue.Enqueue(InboundEvent.FramingFailed(_framer.Error));
            }
        }
    }
}
