using System;
using System.Buffers.Binary;
using System.Text;
using NUnit.Framework;
using TheVerge.Core.Session;
using TheVerge.Network.Framing;
using TheVerge.Network.IO;
using TheVerge.Network.Packets.Chat;
using TheVerge.Network.Session;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Network.Packets.Chat
{
    /// <summary>
    /// 0xAD Unicode Speech так, как его читает сервер (Projects/UOContent/Network/Packets/IncomingMessagePackets.cs,
    /// UnicodeSpeech): byte тип (без флага Encoded 0xC0 — без ключевых слов), short цвет, short шрифт, ReadAscii(4) язык,
    /// ReadBigUniSafe — UTF-16 big-endian до нуля. Длина — переменная (Register(0xAD, 0, …)).
    /// </summary>
    public sealed class SpeechPacketTests
    {
        [Test]
        public void Bytes_AsServerReads()
        {
            var bytes = PacketBytes.Write((ref PacketWriter w) => UnicodeSpeechPacket.Write(ref w, "Hi", 0x02B2));

            Assert.That(bytes, Is.EqualTo(new byte[]
            {
                0xAD, 0x00, 0x12, 0x00, 0x02, 0xB2, 0x00, 0x03, (byte)'E', (byte)'N', (byte)'U', 0x00,
                0x00, (byte)'H', 0x00, (byte)'i', 0x00, 0x00
            }));
            Assert.That(PacketTable.Get(PacketDirection.ClientToServer, 0xAD).IsVariable, Is.True);
        }

        [Test]
        public void Russian_ReadBackByServerRules()
        {
            var bytes = PacketBytes.Write((ref PacketWriter w) => UnicodeSpeechPacket.Write(ref w, "[go Миног", 0x02B2));

            var reader = new PacketReader(bytes.AsSpan(3));
            Assert.That(reader.ReadByte(), Is.EqualTo(0), "тип Regular");
            Assert.That(reader.ReadUInt16(), Is.EqualTo(0x02B2));
            reader.ReadUInt16();
            Assert.That(reader.ReadAscii(4), Is.EqualTo(ClientLimits.Language));
            Assert.That(reader.ReadUtf16BigEndianNullTerminated(), Is.EqualTo("[go Миног"));
            Assert.That(reader.Remaining, Is.EqualTo(0));
            Assert.That(BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(1)), Is.EqualTo(bytes.Length));
        }

        [Test]
        public void SpeechSender_WritesToOutgoing()
        {
            var session = new NetworkSession(new PacketDispatcher(new RecordingLog()), new RecordingLog());

            new SpeechSender(session).SendSpeech("Hi", 0x02B2);

            Assert.That(session.Outgoing.Count, Is.EqualTo(1));
            Assert.That(session.Outgoing.Pending[0], Is.EqualTo(0xAD));
            Assert.That(session.Outgoing.Pending.Length, Is.EqualTo(18));
        }

        [Test]
        public void MaxText_Fits()
        {
            var text = new string('я', ServerLimits.SpeechMaxChars);

            Assert.That(UnicodeSpeechPacket.LengthOf(text), Is.EqualTo(14 + 2 * ServerLimits.SpeechMaxChars));
        }
    }
}
