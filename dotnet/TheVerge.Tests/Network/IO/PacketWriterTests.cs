using System;
using NUnit.Framework;
using TheVerge.Network.IO;

namespace TheVerge.Tests.Network.IO
{
    /// <summary>
    /// Запись полей пакета (PLAN Стадия 1, п. 1). Ожидаемые байты — то, что пишет сервер:
    /// Projects/Server/Buffers/SpanWriter.cs (числа big-endian через BinaryPrimitives; строки — Write(value, encoding,
    /// fixedLength): обрезка по целым символам до fixedLength * размер символа, остаток — нули; *Null — строка и ноль).
    /// Кодировки по пакетам Стадии 1: Latin-1 — логин и пароль 0x80/0x91 (IncomingAccountPackets.cs, ReadLatin1Safe(30)),
    /// имя шарда 0xA8 и города 0xA9 (OutgoingAccountPackets.cs, WriteLatin1); ASCII — версия 0xBD
    /// (IncomingAccountPackets.cs, ReadAscii()) и язык 0xBF 0x0B (ReadAscii(4)); UTF-8 30 байт — имена (E1, ADR 0005).
    /// </summary>
    public sealed class PacketWriterTests
    {
        [Test]
        public void Write_Numbers_AreBigEndian()
        {
            var buffer = new byte[13];
            var writer = new PacketWriter(buffer);

            writer.Write((byte)0xA8);
            writer.Write((ushort)0x1234);
            writer.Write((short)-2);
            writer.Write(0x01020304u);
            writer.Write(-2);

            Assert.That(writer.Position, Is.EqualTo(13));
            Assert.That(buffer, Is.EqualTo(new byte[]
            {
                0xA8, 0x12, 0x34, 0xFF, 0xFE, 0x01, 0x02, 0x03, 0x04, 0xFF, 0xFF, 0xFF, 0xFE,
            }));
        }

        [Test]
        public void Write_BooleanAndSignedByte_AsServer()
        {
            var buffer = new byte[3];
            var writer = new PacketWriter(buffer);

            writer.Write(true);
            writer.Write(false);
            writer.Write((sbyte)-1);

            Assert.That(buffer, Is.EqualTo(new byte[] { 0x01, 0x00, 0xFF }));
        }

        [Test]
        public void WriteLatin1_ShorterThanField_PadsWithZeros()
        {
            var buffer = new byte[32];
            var writer = new PacketWriter(buffer);

            writer.WriteLatin1("Café", 32);

            var expected = new byte[32];
            new byte[] { 0x43, 0x61, 0x66, 0xE9 }.CopyTo(expected, 0);
            Assert.That(buffer, Is.EqualTo(expected));
            Assert.That(writer.Position, Is.EqualTo(32));
        }

        [Test]
        public void WriteLatin1_CharOutsideLatin1_BecomesQuestionMark()
        {
            var buffer = new byte[3];
            var writer = new PacketWriter(buffer);

            writer.WriteLatin1("aЖb", 3);

            Assert.That(buffer, Is.EqualTo(new byte[] { 0x61, 0x3F, 0x62 }));
        }

        [Test]
        public void WriteLatin1_LongerThanField_IsCutToField()
        {
            var buffer = new byte[4];
            var writer = new PacketWriter(buffer);

            writer.WriteLatin1("abcdef", 3);

            Assert.That(writer.Position, Is.EqualTo(3));
            Assert.That(buffer, Is.EqualTo(new byte[] { 0x61, 0x62, 0x63, 0x00 }));
        }

        [Test]
        public void WriteAscii_NonAsciiChar_BecomesQuestionMark()
        {
            var buffer = new byte[4];
            var writer = new PacketWriter(buffer);

            writer.WriteAscii("é1", 4);

            Assert.That(buffer, Is.EqualTo(new byte[] { 0x3F, 0x31, 0x00, 0x00 }));
        }

        [Test]
        public void WriteAsciiNullTerminated_VersionString_EndsWithZero()
        {
            var buffer = new byte[9];
            var writer = new PacketWriter(buffer);

            writer.WriteAsciiNullTerminated("7.0.95.0");

            Assert.That(writer.Position, Is.EqualTo(9));
            Assert.That(buffer, Is.EqualTo(new byte[] { 0x37, 0x2E, 0x30, 0x2E, 0x39, 0x35, 0x2E, 0x30, 0x00 }));
        }

        [Test]
        public void WriteUtf8_CyrillicName_BytesThenZeros()
        {
            var buffer = new byte[30];
            var writer = new PacketWriter(buffer);

            writer.WriteUtf8("Вася", 30);

            var expected = new byte[30];
            new byte[] { 0xD0, 0x92, 0xD0, 0xB0, 0xD1, 0x81, 0xD1, 0x8F }.CopyTo(expected, 0);
            Assert.That(buffer, Is.EqualTo(expected));
            Assert.That(writer.Position, Is.EqualTo(30));
        }

        [Test]
        public void WriteUtf8_LongerThanField_CutByWholeLetters()
        {
            // 20 букв кириллицы = 40 байт. Сервер отбрасывает буквы с конца, пока байтов больше 30: остаётся 15 букв.
            var buffer = new byte[30];
            var writer = new PacketWriter(buffer);

            writer.WriteUtf8(new string('Ж', 20), 30);

            Assert.That(writer.Position, Is.EqualTo(30));
            for (var i = 0; i < 30; i += 2)
            {
                Assert.That(new[] { buffer[i], buffer[i + 1] }, Is.EqualTo(new byte[] { 0xD0, 0x96 }), "байт " + i);
            }
        }

        [Test]
        public void WriteUtf8_LetterDoesNotFit_NotSplitAndPadded()
        {
            // "a" + 15 букв = 31 байт: последняя буква целиком не влезает и отбрасывается, на её месте — ноль.
            var buffer = new byte[30];
            var writer = new PacketWriter(buffer);

            writer.WriteUtf8("a" + new string('Ж', 15), 30);

            Assert.That(writer.Position, Is.EqualTo(30));
            Assert.That(buffer[0], Is.EqualTo(0x61));
            Assert.That(buffer[27], Is.EqualTo(0xD0));
            Assert.That(buffer[28], Is.EqualTo(0x96));
            Assert.That(buffer[29], Is.EqualTo(0x00));
        }

        [Test]
        public void WriteUtf16BigEndianNullTerminated_EndsWithTwoZeros()
        {
            var buffer = new byte[6];
            var writer = new PacketWriter(buffer);

            writer.WriteUtf16BigEndianNullTerminated("Hж");

            Assert.That(writer.Position, Is.EqualTo(6));
            Assert.That(buffer, Is.EqualTo(new byte[] { 0x00, 0x48, 0x04, 0x36, 0x00, 0x00 }));
        }

        [Test]
        public void WriteUtf16BigEndian_FixedChars_PadsWithZeros()
        {
            var buffer = new byte[8];
            var writer = new PacketWriter(buffer);

            writer.WriteUtf16BigEndian("Hi", 4);

            Assert.That(writer.Position, Is.EqualTo(8));
            Assert.That(buffer, Is.EqualTo(new byte[] { 0x00, 0x48, 0x00, 0x69, 0x00, 0x00, 0x00, 0x00 }));
        }

        [Test]
        public void WriteBytesAndZeros_CopyAndClear()
        {
            var buffer = new byte[] { 9, 9, 9, 9, 9 };
            var writer = new PacketWriter(buffer);

            writer.Write(new byte[] { 0x7F, 0x00, 0x00, 0x01 }.AsSpan(0, 2));
            writer.WriteZeros(3);

            Assert.That(buffer, Is.EqualTo(new byte[] { 0x7F, 0x00, 0x00, 0x00, 0x00 }));
            Assert.That(writer.Written.ToArray(), Is.EqualTo(buffer));
        }

        [Test]
        public void Write_PastEnd_ThrowsAndKeepsPosition()
        {
            var buffer = new byte[3];
            var writer = new PacketWriter(buffer);
            writer.Write((byte)1);

            var thrown = false;
            try
            {
                writer.Write(5u);
            }
            catch (PacketOutOfRangeException)
            {
                thrown = true;
            }

            Assert.That(thrown, Is.True);
            Assert.That(writer.Position, Is.EqualTo(1));
            Assert.That(buffer, Is.EqualTo(new byte[] { 1, 0, 0 }));
        }

        [Test]
        public void WriteFixedString_FieldPastEnd_Throws()
        {
            AssertOutOfRange(() =>
            {
                var writer = new PacketWriter(new byte[29]);
                writer.WriteUtf8("a", 30);
            });
        }

        [Test]
        public void WriteZeros_PastEnd_Throws()
        {
            AssertOutOfRange(() =>
            {
                var writer = new PacketWriter(new byte[2]);
                writer.WriteZeros(3);
            });
        }

        [Test]
        public void WriteFixedString_NegativeLength_Throws()
        {
            AssertOutOfRange(() =>
            {
                var writer = new PacketWriter(new byte[4]);
                writer.WriteLatin1("a", -1);
            });
        }

        // У Assert.Throws в NUnit 4 две перегрузки для лямбды; параметр типа Action снимает неоднозначность.
        private static void AssertOutOfRange(Action code) => Assert.Throws<PacketOutOfRangeException>(code);
    }
}
