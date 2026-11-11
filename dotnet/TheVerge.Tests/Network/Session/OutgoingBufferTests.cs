using System;
using NUnit.Framework;
using TheVerge.Network.IO;
using TheVerge.Network.Session;

namespace TheVerge.Tests.Network.Session
{
    /// <summary>
    /// Буфер исходящих за тик (AGENTS.md §2.3, исходящее, шаги 3–4): отправители дописывают пакеты, в конце тика всё
    /// уходит одним вызовом. Каждый пакет сверяется с матрицей клиент → сервер (PacketTable): ошибка отправителя
    /// видна сразу, а не на сервере.
    /// </summary>
    public sealed class OutgoingBufferTests
    {
        [Test]
        public void TwoPackets_PendingHasBothInOrder()
        {
            var buffer = new OutgoingBuffer();

            Write(buffer, new byte[] { 0x73, 0x05 });
            Write(buffer, new byte[] { 0xD1, 0x00 });

            Assert.That(buffer.Pending.ToArray(), Is.EqualTo(new byte[] { 0x73, 0x05, 0xD1, 0x00 }));
        }

        [Test]
        public void ManyPackets_BufferGrows()
        {
            var buffer = new OutgoingBuffer();
            var packet = new byte[] { 0x73, 0x01 };

            for (var i = 0; i < 10000; i++)
            {
                Write(buffer, packet);
            }

            Assert.That(buffer.Pending.Length, Is.EqualTo(20000));
        }

        [Test]
        public void VariablePacket_WrittenThroughWriter()
        {
            // 0xBD от клиента: номер, длина, строка версии с нулём (IncomingAccountPackets.cs, ClientVersion).
            var buffer = new OutgoingBuffer();
            var writer = new PacketWriter(buffer.Reserve(64));
            writer.Write((byte)0xBD);
            writer.Write((ushort)12);
            writer.WriteAsciiNullTerminated("7.0.95.0");

            buffer.Commit(writer.Position);

            Assert.That(buffer.Pending.Length, Is.EqualTo(12));
        }

        [Test]
        public void Clear_EmptiesPending()
        {
            var buffer = new OutgoingBuffer();
            Write(buffer, new byte[] { 0x73, 0x05 });

            buffer.Clear();

            Assert.That(buffer.Pending.Length, Is.EqualTo(0));
        }

        [Test]
        public void Commit_UnknownClientPacket_Throws()
        {
            // 0x1B сервер шлёт, а от клиента такого пакета нет.
            AssertRejected(new byte[] { 0x1B, 0x00 });
        }

        [Test]
        public void Commit_FixedLengthMismatch_Throws()
        {
            AssertRejected(new byte[] { 0x73, 0x01, 0x02 });
        }

        [Test]
        public void Commit_VariableLengthFieldMismatch_Throws()
        {
            AssertRejected(new byte[] { 0xBD, 0x00, 0x05, 0x37 });
        }

        [Test]
        public void Commit_MoreThanReserved_Throws()
        {
            var buffer = new OutgoingBuffer();
            buffer.Reserve(2);

            Assert.Throws<InvalidOperationException>(new Action(() => buffer.Commit(3)));
        }

        [Test]
        public void Rejected_LeavesPendingUnchanged()
        {
            var buffer = new OutgoingBuffer();
            Write(buffer, new byte[] { 0x73, 0x05 });

            Assert.Throws<InvalidOperationException>(new Action(() => Write(buffer, new byte[] { 0x73, 0x01, 0x02 })));
            Assert.That(buffer.Pending.ToArray(), Is.EqualTo(new byte[] { 0x73, 0x05 }));
        }

        private static void AssertRejected(byte[] packet)
        {
            var buffer = new OutgoingBuffer();

            Assert.Throws<InvalidOperationException>(new Action(() => Write(buffer, packet)));
        }

        private static void Write(OutgoingBuffer buffer, byte[] packet)
        {
            packet.CopyTo(buffer.Reserve(packet.Length));
            buffer.Commit(packet.Length);
        }
    }
}
