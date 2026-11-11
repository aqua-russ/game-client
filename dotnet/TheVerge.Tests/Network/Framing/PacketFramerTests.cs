using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TheVerge.Core.Session;
using TheVerge.Network.Framing;

namespace TheVerge.Tests.Network.Framing
{
    /// <summary>
    /// Нарезка потока сервер → клиент на целые пакеты (PLAN Стадия 1, п. 2). Длины — из PacketTable; длина переменного
    /// пакета — 2 байта после номера (Projects/Server/Network/PacketUtilities.cs, WritePacketLength); меньше 3 сервер
    /// и сам считает ошибкой (NetState.cs, HandlePacket). Отдельного предела длины у сервера нет: верхний предел —
    /// само поле длины, 65 535 (ADR 0013, docs/TARGET.md §3.1). Неизвестный номер или
    /// подкоманда и сломанная длина — явная ошибка с дампом (AGENTS.md §3.5, docs/protocol/packets.md).
    /// </summary>
    public sealed class PacketFramerTests
    {
        [Test]
        public void WholeStream_ReturnsServerPacketsInOrder()
        {
            AssertSameAsServer(stream => new[] { stream.Length });
        }

        [Test]
        public void StreamByOneByte_ReturnsSamePackets()
        {
            AssertSameAsServer(stream => Enumerable.Repeat(1, stream.Length));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(7)]
        [TestCase(40)]
        public void StreamInEqualChunks_ReturnsSamePackets(int chunk)
        {
            AssertSameAsServer(stream => EqualChunks(stream.Length, chunk));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void StreamInRandomChunks_ReturnsSamePackets(int seed)
        {
            var random = new Random(seed);
            AssertSameAsServer(stream => RandomChunks(stream.Length, random));
        }

        [Test]
        public void PartOfPacket_WaitsForRest()
        {
            var packet = ServerPacketSamples.LoginConfirm();
            var framer = new PacketFramer(PacketDirection.ServerToClient);

            framer.Append(packet.AsSpan(0, 20));
            Assert.That(framer.Next(out _), Is.EqualTo(FrameStatus.NeedMore));

            framer.Append(packet.AsSpan(20));
            Assert.That(framer.Next(out var frame), Is.EqualTo(FrameStatus.Packet));
            Assert.That(frame.ToArray(), Is.EqualTo(packet));
            Assert.That(framer.Next(out _), Is.EqualTo(FrameStatus.NeedMore));
        }

        [Test]
        public void HalfOfLengthField_WaitsForRest()
        {
            var framer = new PacketFramer(PacketDirection.ServerToClient);

            framer.Append(new byte[] { 0xA8, 0x00 });

            Assert.That(framer.Next(out _), Is.EqualTo(FrameStatus.NeedMore));
        }

        [Test]
        public void UnknownId_AfterGoodPacket_ErrorWithDumpAndStops()
        {
            // 0x02 — пакет только клиент → сервер, от сервера его нет в матрице.
            var framer = new PacketFramer(PacketDirection.ServerToClient);
            framer.Append(new byte[] { 0x55, 0x02, 0x01, 0x02, 0x03 });

            Assert.That(framer.Next(out var first), Is.EqualTo(FrameStatus.Packet));
            Assert.That(first.ToArray(), Is.EqualTo(new byte[] { 0x55 }));
            Assert.That(framer.Next(out _), Is.EqualTo(FrameStatus.Error));
            Assert.That(framer.Error.Kind, Is.EqualTo(FramingErrorKind.UnknownPacket));
            Assert.That(framer.Error.Id, Is.EqualTo(0x02));
            Assert.That(framer.Error.Dump, Is.EqualTo(new byte[] { 0x02, 0x01, 0x02, 0x03 }));

            Assert.That(framer.Append(new byte[] { 0x55 }), Is.EqualTo(0));
            Assert.That(framer.Next(out _), Is.EqualTo(FrameStatus.Error));
        }

        [Test]
        public void UnknownId_DumpLimited()
        {
            var data = new byte[1000];
            data[0] = 0x02;
            var framer = new PacketFramer(PacketDirection.ServerToClient);
            framer.Append(data);

            Assert.That(framer.Next(out _), Is.EqualTo(FrameStatus.Error));
            Assert.That(framer.Error.Dump.Length, Is.EqualTo(ClientLimits.ErrorDumpBytes));
            Assert.That(framer.Error.Dump[0], Is.EqualTo(0x02));
        }

        [Test]
        public void UnknownExtendedSubCommand_ErrorWithWholePacket()
        {
            var packet = new byte[] { 0xBF, 0x00, 0x05, 0x00, 0x01 };

            var error = FrameUntilError(packet);

            Assert.That(error.Kind, Is.EqualTo(FramingErrorKind.UnknownSubCommand));
            Assert.That(error.Id, Is.EqualTo(0xBF));
            Assert.That(error.SubCommand, Is.EqualTo(0x0001));
            Assert.That(error.Dump, Is.EqualTo(packet));
        }

        [Test]
        public void ExtendedSubCommand_IsTwoBytes()
        {
            // 0x19 (ExtendedStats) есть в матрице, а 0x0119 — нет: старший байт подкоманды не теряется.
            var error = FrameUntilError(new byte[] { 0xBF, 0x00, 0x05, 0x01, 0x19 });

            Assert.That(error.Kind, Is.EqualTo(FramingErrorKind.UnknownSubCommand));
            Assert.That(error.SubCommand, Is.EqualTo(0x0119));
        }

        [Test]
        public void UnknownAssistantSubCommand_Error()
        {
            var error = FrameUntilError(new byte[] { 0xF0, 0x00, 0x04, 0x03 });

            Assert.That(error.Kind, Is.EqualTo(FramingErrorKind.UnknownSubCommand));
            Assert.That(error.Id, Is.EqualTo(0xF0));
            Assert.That(error.SubCommand, Is.EqualTo(0x03));
        }

        [Test]
        public void UnknownSubCommand_WaitsForWholePacketBeforeError()
        {
            var packet = new byte[] { 0xBF, 0x00, 0x08, 0x00, 0x01, 0xAA, 0xBB, 0xCC };
            var framer = new PacketFramer(PacketDirection.ServerToClient);

            framer.Append(packet.AsSpan(0, 5));
            Assert.That(framer.Next(out _), Is.EqualTo(FrameStatus.NeedMore));

            framer.Append(packet.AsSpan(5));
            Assert.That(framer.Next(out _), Is.EqualTo(FrameStatus.Error));
            Assert.That(framer.Error.Dump, Is.EqualTo(packet));
        }

        [TestCase(new byte[] { 0xA8, 0x00, 0x02 }, 2)]
        [TestCase(new byte[] { 0xA8, 0x00, 0x00 }, 0)]
        [TestCase(new byte[] { 0xBF, 0x00, 0x04, 0x00 }, 4)]
        [TestCase(new byte[] { 0xF0, 0x00, 0x03 }, 3)]
        public void LengthShorterThanHeader_BadLength(byte[] data, int declared)
        {
            var error = FrameUntilError(data);

            Assert.That(error.Kind, Is.EqualTo(FramingErrorKind.BadLength));
            Assert.That(error.Id, Is.EqualTo(data[0]));
            Assert.That(error.DeclaredLength, Is.EqualTo(declared));
            Assert.That(error.Dump, Is.EqualTo(data));
        }

        [Test]
        public void LargestPossiblePacket_PassesWhole()
        {
            var packet = new byte[ushort.MaxValue];
            packet[0] = 0x1C;
            packet[1] = 0xFF;
            packet[2] = 0xFF;
            packet[ushort.MaxValue - 1] = 0x77;
            var stream = packet.Concat(ServerPacketSamples.LoginComplete()).ToArray();

            var frames = Frame(stream, EqualChunks(stream.Length, 1000));

            Assert.That(frames.Count, Is.EqualTo(2));
            Assert.That(frames[0], Is.EqualTo(packet));
            Assert.That(frames[1], Is.EqualTo(ServerPacketSamples.LoginComplete()));
        }

        private static void AssertSameAsServer(Func<byte[], IEnumerable<int>> chunks)
        {
            var expected = ServerPacketSamples.LoginStream();
            var stream = ServerPacketSamples.Concat(expected);

            var frames = Frame(stream, chunks(stream));

            Assert.That(frames, Is.EqualTo(expected));
        }

        private static List<byte[]> Frame(byte[] stream, IEnumerable<int> chunks)
        {
            var framer = new PacketFramer(PacketDirection.ServerToClient);
            var frames = new List<byte[]>();
            var offset = 0;
            foreach (var size in chunks)
            {
                var chunk = stream.AsSpan(offset, size);
                offset += size;
                while (!chunk.IsEmpty)
                {
                    chunk = chunk.Slice(framer.Append(chunk));
                    Drain(framer, frames);
                }
            }

            Assert.That(offset, Is.EqualTo(stream.Length), "нарезка теста не покрыла поток");
            return frames;
        }

        private static void Drain(PacketFramer framer, List<byte[]> frames)
        {
            FrameStatus status;
            while ((status = framer.Next(out var frame)) == FrameStatus.Packet)
            {
                frames.Add(frame.ToArray());
            }

            Assert.That(status, Is.EqualTo(FrameStatus.NeedMore), "ошибка нарезки: " + framer.Error.Kind);
        }

        private static FramingError FrameUntilError(byte[] data)
        {
            var framer = new PacketFramer(PacketDirection.ServerToClient);
            framer.Append(data);

            Assert.That(framer.Next(out _), Is.EqualTo(FrameStatus.Error));
            return framer.Error;
        }

        private static IEnumerable<int> EqualChunks(int total, int chunk)
        {
            for (var offset = 0; offset < total; offset += chunk)
            {
                yield return Math.Min(chunk, total - offset);
            }
        }

        private static IEnumerable<int> RandomChunks(int total, Random random)
        {
            var offset = 0;
            while (offset < total)
            {
                var size = Math.Min(random.Next(1, 64), total - offset);
                offset += size;
                yield return size;
            }
        }
    }
}
