using System.IO;

namespace TheVerge.Tests.Data.Map
{
    /// <summary>Поток-обёртка: считает прочитанные байты (проверка «файл не читается целиком»).</summary>
    internal sealed class CountingStream : Stream
    {
        private readonly Stream _inner;

        public CountingStream(Stream inner) => _inner = inner;

        public long BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void Flush() { }
        public override void SetLength(long value) => throw new System.NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new System.NotSupportedException();
    }
}
