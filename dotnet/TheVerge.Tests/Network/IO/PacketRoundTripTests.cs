using System;
using NUnit.Framework;
using TheVerge.Network.IO;

namespace TheVerge.Tests.Network.IO
{
    /// <summary>
    /// Запись, а затем чтение возвращает те же значения для каждого типа поля (PLAN Стадия 1, п. 1, Тест).
    /// Байтовый формат проверяют PacketWriterTests и PacketReaderTests по серверу; здесь — что чтение и запись согласованы
    /// на множестве значений, включая крайние.
    /// </summary>
    public sealed class PacketRoundTripTests
    {
        private const int Samples = 500;

        [TestCase(1)]
        [TestCase(2)]
        public void Numbers_RoundTrip(int seed)
        {
            var random = new Random(seed);
            var buffer = new byte[15];
            for (var i = 0; i < Samples; i++)
            {
                var b = (byte)random.Next(256);
                var sb = (sbyte)random.Next(-128, 128);
                var us = (ushort)random.Next(65536);
                var s = (short)random.Next(short.MinValue, short.MaxValue + 1);
                var ui = (uint)random.Next() ^ ((uint)random.Next(2) << 31);
                var n = random.Next(int.MinValue, int.MaxValue);
                var flag = random.Next(2) == 1;

                var writer = new PacketWriter(buffer);
                writer.Write(b);
                writer.Write(sb);
                writer.Write(us);
                writer.Write(s);
                writer.Write(ui);
                writer.Write(n);
                writer.Write(flag);

                var reader = new PacketReader(buffer);
                Assert.That(reader.ReadByte(), Is.EqualTo(b));
                Assert.That(reader.ReadSByte(), Is.EqualTo(sb));
                Assert.That(reader.ReadUInt16(), Is.EqualTo(us));
                Assert.That(reader.ReadInt16(), Is.EqualTo(s));
                Assert.That(reader.ReadUInt32(), Is.EqualTo(ui));
                Assert.That(reader.ReadInt32(), Is.EqualTo(n));
                Assert.That(reader.ReadBoolean(), Is.EqualTo(flag));
            }
        }

        [Test]
        public void ExtremeNumbers_RoundTrip()
        {
            var buffer = new byte[12];
            var writer = new PacketWriter(buffer);
            writer.Write(ushort.MaxValue);
            writer.Write(short.MinValue);
            writer.Write(uint.MaxValue);
            writer.Write(int.MinValue);

            var reader = new PacketReader(buffer);
            Assert.That(reader.ReadUInt16(), Is.EqualTo(ushort.MaxValue));
            Assert.That(reader.ReadInt16(), Is.EqualTo(short.MinValue));
            Assert.That(reader.ReadUInt32(), Is.EqualTo(uint.MaxValue));
            Assert.That(reader.ReadInt32(), Is.EqualTo(int.MinValue));
        }

        [Test]
        public void Latin1AndAscii_RoundTrip()
        {
            var random = new Random(3);
            var buffer = new byte[64];
            for (var i = 0; i < Samples; i++)
            {
                var latin1 = RandomString(random, random.Next(0, 30), 0x01, 0xFF);
                var ascii = RandomString(random, random.Next(0, 30), 0x01, 0x7F);

                var writer = new PacketWriter(buffer);
                writer.WriteLatin1(latin1, 30);
                writer.WriteAsciiNullTerminated(ascii);

                var reader = new PacketReader(buffer.AsSpan(0, writer.Position));
                Assert.That(reader.ReadLatin1(30), Is.EqualTo(latin1));
                Assert.That(reader.ReadAsciiNullTerminated(), Is.EqualTo(ascii));
            }
        }

        [Test]
        public void Utf8Names_RoundTrip()
        {
            // Правило имени E1 (docs/protocol/extensions.md): не больше 29 байт UTF-8, поэтому в поле 30 байт есть ноль.
            var random = new Random(4);
            var buffer = new byte[30];
            for (var i = 0; i < Samples; i++)
            {
                var name = RandomString(random, random.Next(2, 15), 0x0410, 0x044F);

                var writer = new PacketWriter(buffer);
                writer.WriteUtf8(name, 30);

                Assert.That(new PacketReader(buffer).ReadUtf8(30), Is.EqualTo(name));
            }
        }

        [Test]
        public void Utf16BigEndian_RoundTrip()
        {
            var random = new Random(5);
            var buffer = new byte[200];
            for (var i = 0; i < Samples; i++)
            {
                var text = RandomString(random, random.Next(0, 40), 0x0020, 0xD7FF);

                var writer = new PacketWriter(buffer);
                writer.WriteUtf16BigEndianNullTerminated(text);
                writer.WriteUtf16BigEndian(text, 40);

                var reader = new PacketReader(buffer.AsSpan(0, writer.Position));
                Assert.That(reader.ReadUtf16BigEndianNullTerminated(), Is.EqualTo(text));
                Assert.That(reader.ReadUtf16BigEndian(40), Is.EqualTo(text));
                Assert.That(reader.Remaining, Is.EqualTo(0));
            }
        }

        private static string RandomString(Random random, int length, int minChar, int maxChar)
        {
            var chars = new char[length];
            for (var i = 0; i < length; i++)
            {
                chars[i] = (char)random.Next(minChar, maxChar + 1);
            }

            return new string(chars);
        }
    }
}
