#nullable disable // долг прототипа: файл заменяется по docs/TECH-DEBT.md, разметку nullable не делаем
using System;
using System.Buffers.Binary;
using System.Text;
using TheVerge.Core;

namespace TheVerge.Network.Processors
{
    public sealed class GumpAndMessagePacketProcessor
    {
        private readonly IMainThreadDispatcher _dispatcher;
        private readonly Action<byte[]> _sendRawAction;

        public event Action<OverheadMessageDto> OnOverheadMessage;
        public event Action<string> OnGumpWindowOpened;
        public event Action<VisualEffectDto> OnVisualEffect;

        public GumpAndMessagePacketProcessor(IMainThreadDispatcher dispatcher, Action<byte[]> sendRawAction)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _sendRawAction = sendRawAction ?? throw new ArgumentNullException(nameof(sendRawAction));
        }

        public void ParseAsciiSpeech(ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 44) return;
            uint sourceSerial = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(3, 4));
            ushort hue = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(10, 2));
            int len = packet.Slice(44).IndexOf((byte)0);
            if (len < 0) len = packet.Slice(44).Length;
            string text = len > 0 ? Encoding.UTF8.GetString(packet.Slice(44, len)).Trim() : string.Empty;
            if (!string.IsNullOrEmpty(text))
            {
                DispatchSpeechOrUiTrigger(new Serial(sourceSerial), text, hue);
            }
        }

        public void ParseUnicodeSpeech(ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 48) return;
            uint sourceSerial = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(3, 4));
            ushort hue = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(10, 2));
            var textSpan = packet.Slice(48);
            int byteLen = textSpan.Length;
            if (byteLen >= 2 && textSpan[byteLen - 1] == 0 && textSpan[byteLen - 2] == 0) byteLen -= 2;
            if (byteLen <= 0) return;

            string text = Encoding.BigEndianUnicode.GetString(textSpan.Slice(0, byteLen)).Trim('\0');
            if (!string.IsNullOrEmpty(text))
            {
                DispatchSpeechOrUiTrigger(new Serial(sourceSerial), text, hue);
            }
        }

        private void DispatchSpeechOrUiTrigger(Serial sourceSerial, string text, ushort hue)
        {
            if (text == "[UI:CRAFT]")
            {
                _dispatcher.Enqueue(() => OnGumpWindowOpened?.Invoke("craft"));
                return;
            }
            _dispatcher.Enqueue(() => OnOverheadMessage?.Invoke(new OverheadMessageDto(sourceSerial, text, hue)));
        }

        public void ParseGump(ReadOnlySpan<byte> packet)
        {
            _dispatcher.Enqueue(() => OnGumpWindowOpened?.Invoke("craft"));
        }

        public void ParseGraphicalEffect(ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 28) return;
            byte type = packet[1];
            uint srcSer = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(2, 4));
            uint dstSer = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(6, 4));
            ushort itemId = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(10, 2));
            short sx = BinaryPrimitives.ReadInt16BigEndian(packet.Slice(12, 2));
            short sy = BinaryPrimitives.ReadInt16BigEndian(packet.Slice(14, 2));
            sbyte sz = (sbyte)packet[16];
            short dx = BinaryPrimitives.ReadInt16BigEndian(packet.Slice(17, 2));
            short dy = BinaryPrimitives.ReadInt16BigEndian(packet.Slice(19, 2));
            sbyte dz = (sbyte)packet[21];

            var fx = new VisualEffectDto(
                type, new Serial(srcSer), new Serial(dstSer), itemId,
                new Point3D(sx, sy, sz), new Point3D(dx, dy, dz));
            _dispatcher.Enqueue(() => OnVisualEffect?.Invoke(fx));
        }

        public void SendAsciiCommand(string commandText)
        {
            if (string.IsNullOrEmpty(commandText)) return;
            byte[] asciiBytes = Encoding.ASCII.GetBytes(commandText);
            int length = 8 + asciiBytes.Length + 1;
            byte[] packet = new byte[length];
            packet[0] = 0x03;
            NetByteConverter.WriteUInt16(packet, 1, (ushort)length);
            packet[3] = 0x00;
            NetByteConverter.WriteUInt16(packet, 4, 0x03B2);
            NetByteConverter.WriteUInt16(packet, 6, 3);
            Array.Copy(asciiBytes, 0, packet, 8, asciiBytes.Length);
            packet[length - 1] = 0;
            _sendRawAction(packet);
        }
    }
}
