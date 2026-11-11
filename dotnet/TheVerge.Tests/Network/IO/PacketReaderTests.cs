using System;
using NUnit.Framework;
using TheVerge.Network.IO;

namespace TheVerge.Tests.Network.IO
{
    /// <summary>
    /// Чтение полей пакета (PLAN Стадия 1, п. 1). Ожидания — поведение сервера:
    /// Projects/Server/Buffers/SpanReader.cs (числа big-endian; строка фиксированной длины кончается на первом нуле,
    /// но позиция сдвигается на всё поле; строка без длины — до нуля или до конца пакета; у UTF-16 ноль ищется только
    /// на чётном месте — StringHelpers.IndexOfTerminator) и Projects/Server/Text/TextEncoding.cs (без «safe»: ASCII
    /// заменяет байты больше 0x7F на «?», UTF-8 — битые байты на U+FFFD). Чтение за концом — ошибка, а не мусор.
    /// </summary>
    public sealed class PacketReaderTests
    {
        [Test]
        public void ReadNumbers_BigEndian()
        {
            var reader = new PacketReader(new byte[]
            {
                0xA8, 0x12, 0x34, 0xFF, 0xFE, 0x01, 0x02, 0x03, 0x04, 0xFF, 0xFF, 0xFF, 0xFE, 0x80, 0x02,
            });

            Assert.That(reader.ReadByte(), Is.EqualTo(0xA8));
            Assert.That(reader.ReadUInt16(), Is.EqualTo(0x1234));
            Assert.That(reader.ReadInt16(), Is.EqualTo(-2));
            Assert.That(reader.ReadUInt32(), Is.EqualTo(0x01020304u));
            Assert.That(reader.ReadInt32(), Is.EqualTo(-2));
            Assert.That(reader.ReadSByte(), Is.EqualTo(-128));
            Assert.That(reader.ReadBoolean(), Is.True);
            Assert.That(reader.Remaining, Is.EqualTo(0));
            Assert.That(reader.Position, Is.EqualTo(15));
            Assert.That(reader.Length, Is.EqualTo(15));
        }

        [Test]
        public void ReadServerListEntry_AsServerWritesIt()
        {
            // Запись шарда в 0xA8 — OutgoingAccountPackets.cs, SendAccountLoginAck: индекс (ushort), имя
            // WriteLatin1(32), процент заполнения, часовой пояс (sbyte), адрес Write(RawAddress) — big-endian.
            // RawAddress для 127.0.0.1 = 0x0100007F (ServerInfo.cs: ReadUInt32LittleEndian от байтов адреса).
            var data = new byte[40];
            data[1] = 0x00;
            new byte[] { 0x54, 0x68, 0x65, 0x20, 0x56, 0x65, 0x72, 0x67, 0x65 }.CopyTo(data, 2);
            data[34] = 0x00;
            data[35] = 0xFD;
            new byte[] { 0x01, 0x00, 0x00, 0x7F }.CopyTo(data, 36);
            var reader = new PacketReader(data);

            Assert.That(reader.ReadUInt16(), Is.EqualTo(0));
            Assert.That(reader.ReadLatin1(32), Is.EqualTo("The Verge"));
            Assert.That(reader.ReadByte(), Is.EqualTo(0));
            Assert.That(reader.ReadSByte(), Is.EqualTo(-3));
            Assert.That(reader.ReadUInt32(), Is.EqualTo(0x0100007Fu));
            Assert.That(reader.Remaining, Is.EqualTo(0));
        }

        [Test]
        public void ReadUtf8_CyrillicName_StopsAtZeroAndSkipsField()
        {
            var data = new byte[31];
            new byte[] { 0xD0, 0x92, 0xD0, 0xB0, 0xD1, 0x81, 0xD1, 0x8F }.CopyTo(data, 0);
            data[30] = 0x42;
            var reader = new PacketReader(data);

            Assert.That(reader.ReadUtf8(30), Is.EqualTo("Вася"));
            Assert.That(reader.Position, Is.EqualTo(30));
            Assert.That(reader.ReadByte(), Is.EqualTo(0x42));
        }

        [Test]
        public void ReadUtf8_FieldWithoutZero_ReadsWholeField()
        {
            var data = new byte[30];
            for (var i = 0; i < 30; i += 2)
            {
                data[i] = 0xD0;
                data[i + 1] = 0x96;
            }

            var reader = new PacketReader(data);

            Assert.That(reader.ReadUtf8(30), Is.EqualTo(new string('Ж', 15)));
        }

        [Test]
        public void ReadUtf8_BrokenBytes_BecomeReplacementChar()
        {
            var reader = new PacketReader(new byte[] { 0x41, 0xD0, 0x00 });

            Assert.That(reader.ReadUtf8(3), Is.EqualTo("A�"));
        }

        [Test]
        public void ReadAscii_ByteAbove7F_BecomesQuestionMark()
        {
            var reader = new PacketReader(new byte[] { 0x65, 0x6E, 0xE9, 0x00 });

            Assert.That(reader.ReadAscii(4), Is.EqualTo("en?"));
        }

        [Test]
        public void ReadLatin1_HighBytes_MapOneToOne()
        {
            var reader = new PacketReader(new byte[] { 0x43, 0x61, 0x66, 0xE9, 0xFF });

            Assert.That(reader.ReadLatin1(5), Is.EqualTo("Caféÿ"));
        }

        [Test]
        public void ReadAsciiNullTerminated_StopsAfterZero()
        {
            var reader = new PacketReader(new byte[] { 0x37, 0x2E, 0x30, 0x00, 0x99 });

            Assert.That(reader.ReadAsciiNullTerminated(), Is.EqualTo("7.0"));
            Assert.That(reader.Position, Is.EqualTo(4));
        }

        [Test]
        public void ReadNullTerminated_NoZero_ReadsToEnd()
        {
            var reader = new PacketReader(new byte[] { 0x41, 0x42 });

            Assert.That(reader.ReadLatin1NullTerminated(), Is.EqualTo("AB"));
            Assert.That(reader.Remaining, Is.EqualTo(0));
        }

        [Test]
        public void ReadUtf16BigEndianNullTerminated_ZeroOnlyAtEvenPlace()
        {
            // 01 00 | 00 41 | 00 00: нулевая пара на нечётном месте (байты 1–2) — не конец строки.
            var reader = new PacketReader(new byte[] { 0x01, 0x00, 0x00, 0x41, 0x00, 0x00, 0x7A });

            Assert.That(reader.ReadUtf16BigEndianNullTerminated(), Is.EqualTo("ĀA"));
            Assert.That(reader.Position, Is.EqualTo(6));
        }

        [Test]
        public void ReadUtf16BigEndianNullTerminated_OddTail_LeavesLastByte()
        {
            var reader = new PacketReader(new byte[] { 0x00, 0x48, 0x04 });

            Assert.That(reader.ReadUtf16BigEndianNullTerminated(), Is.EqualTo("H"));
            Assert.That(reader.Position, Is.EqualTo(2));
        }

        [Test]
        public void ReadUtf16BigEndian_FixedChars_StopsAtZeroAndSkipsField()
        {
            var reader = new PacketReader(new byte[] { 0x00, 0x48, 0x04, 0x36, 0x00, 0x00, 0x00, 0x69 });

            Assert.That(reader.ReadUtf16BigEndian(4), Is.EqualTo("Hж"));
            Assert.That(reader.Position, Is.EqualTo(8));
        }

        [Test]
        public void ReadFixedString_ZeroLength_ReturnsEmptyAndStays()
        {
            var reader = new PacketReader(new byte[] { 0x41 });

            Assert.That(reader.ReadLatin1(0), Is.EqualTo(string.Empty));
            Assert.That(reader.Position, Is.EqualTo(0));
        }

        [Test]
        public void ReadBytesAndSkip_MovePosition()
        {
            var reader = new PacketReader(new byte[] { 1, 2, 3, 4, 5 });
            var target = new byte[2];

            reader.Skip(1);
            reader.ReadBytes(target);

            Assert.That(target, Is.EqualTo(new byte[] { 2, 3 }));
            Assert.That(reader.ReadByte(), Is.EqualTo(4));
        }

        [Test]
        public void ReadPastEnd_ThrowsAndKeepsPosition()
        {
            var reader = new PacketReader(new byte[] { 1, 2, 3, 4 });
            reader.ReadByte();

            var thrown = false;
            try
            {
                reader.ReadUInt32();
            }
            catch (PacketOutOfRangeException)
            {
                thrown = true;
            }

            Assert.That(thrown, Is.True);
            Assert.That(reader.Position, Is.EqualTo(1));
            Assert.That(reader.ReadByte(), Is.EqualTo(2));
        }

        [Test]
        public void ReadEmpty_ByteAndShort_Throw()
        {
            AssertOutOfRange(() => new PacketReader(Array.Empty<byte>()).ReadByte());
            AssertOutOfRange(() => new PacketReader(new byte[1]).ReadUInt16());
            AssertOutOfRange(() => new PacketReader(new byte[3]).ReadInt32());
        }

        [Test]
        public void ReadFixedString_FieldPastEnd_Throws()
        {
            AssertOutOfRange(() => new PacketReader(new byte[31]).ReadLatin1(32));
            AssertOutOfRange(() => new PacketReader(new byte[29]).ReadUtf8(30));
            AssertOutOfRange(() => new PacketReader(new byte[7]).ReadUtf16BigEndian(4));
            AssertOutOfRange(() => new PacketReader(new byte[3]).ReadAscii(4));
        }

        [Test]
        public void SkipOrReadBytesPastEnd_Throws()
        {
            AssertOutOfRange(() => new PacketReader(new byte[2]).Skip(3));
            AssertOutOfRange(() => new PacketReader(new byte[2]).ReadBytes(new byte[3]));
        }

        [Test]
        public void NegativeLength_Throws()
        {
            AssertOutOfRange(() => new PacketReader(new byte[2]).Skip(-1));
            AssertOutOfRange(() => new PacketReader(new byte[2]).ReadAscii(-1));
        }

        // У Assert.Throws в NUnit 4 две перегрузки для лямбды; параметр типа Action снимает неоднозначность.
        private static void AssertOutOfRange(Action code) => Assert.Throws<PacketOutOfRangeException>(code);
    }
}
