using System.Buffers;

namespace TheVerge.Tests.Network.Transport
{
    /// <summary>Пул буферов, который считает выдачу и возврат: проверка, что ни один буфер пакета не потерян.</summary>
    internal sealed class CountingArrayPool : ArrayPool<byte>
    {
        private readonly object _gate = new object();

        public int Rented { get; private set; }

        public int Returned { get; private set; }

        public int Outstanding
        {
            get
            {
                lock (_gate)
                {
                    return Rented - Returned;
                }
            }
        }

        public override byte[] Rent(int minimumLength)
        {
            lock (_gate)
            {
                Rented++;
            }

            return new byte[minimumLength];
        }

        public override void Return(byte[] array, bool clearArray = false)
        {
            lock (_gate)
            {
                Returned++;
            }
        }
    }
}
