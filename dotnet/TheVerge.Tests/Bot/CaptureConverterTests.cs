using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Framing;
using TheVerge.Network.Transport;
using TheVerge.Tests.Network.Compression;
using TheVerge.Tests.Network.Diagnostics;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Запись посредника (tools/golden/proxy.py, PLAN Стадия 0, п. 6) → лог пакетов в нашем формате. Запись — сырые байты
    /// каждого соединения в обе стороны. Соединение входа начинается с 0xEF (seed), ответы сервера на нём не сжаты;
    /// игровое — с 4 байт ключа и 0x91, всё от сервера на нём сжато Huffman (IncomingAccountPackets.cs: CompressionEnabled
    /// после GameLogin) и распаковывается нашим декодером. Пароль в логе — звёздочками. Сломанный поток — ошибка с местом.
    /// </summary>
    public sealed class CaptureConverterTests
    {
        private const uint Key = 0xE6A23F31;

        [Test]
        public void LoginConnection_PacketsInOrder_PasswordMasked()
        {
            var seed = Seed();
            var login = ClientLoginSamples.AccountLogin();
            var serverList = ServerList();
            var redirect = Redirect();
            var capture = new CaptureBuilder()
                .Open(0, 1)
                .C2S(5, 1, seed.Concat(login.Take(10)).ToArray())
                .C2S(6, 1, login.Skip(10).ToArray())
                .S2C(20, 1, serverList)
                .C2S(30, 1, new byte[] { 0xA0, 0x00, 0x00 })
                .S2C(40, 1, redirect)
                .Close(41, 1, "s");

            var result = Convert(capture, out var entries, out var text);

            Assert.That(result.Errors, Is.Empty);
            Assert.That(entries.Select(Describe), Is.EqualTo(new[]
            {
                "0 CONNECTED", "5 C>S EF", "6 C>S 80", "20 S>C A8", "30 C>S A0", "40 S>C 8C", "41 CLOSED ServerClosed",
            }));
            Assert.That(entries[2].IsMasked, Is.True);
            Assert.That(text, Does.Not.Contain(Hex(Encoding.ASCII.GetBytes(ClientLoginSamples.Password))));
            Assert.That(entries[5].Packet, Is.EqualTo(redirect));
            Assert.That((result.Connections, result.ClientPackets, result.ServerPackets), Is.EqualTo((1, 3, 2)));
        }

        [Test]
        public void GameConnection_KeySkipped_ServerStreamDecompressed()
        {
            var features = new byte[] { 0xB9, 0x00, 0x00, 0x00, 0x03 };
            var ping = new byte[] { 0x73, 0x07 };
            var first = HuffmanTestEncoder.Compress(features);
            Assume.That(first.Length, Is.GreaterThanOrEqualTo(2), "первый пакет должен разрезаться между кусками");
            var compressed = HuffmanTestEncoder.CompressEach(new[] { features, ping });
            var gameLogin = ClientLoginSamples.GameLogin(Key);
            var capture = new CaptureBuilder()
                .Open(0, 2)
                .C2S(1, 2, KeyBytes().Take(2).ToArray())
                .C2S(2, 2, KeyBytes().Skip(2).Concat(gameLogin.Take(3)).ToArray())
                .C2S(3, 2, gameLogin.Skip(3).ToArray())
                .S2C(10, 2, compressed.Take(1).ToArray())
                .S2C(11, 2, compressed.Skip(1).ToArray())
                .Close(20, 2, "c");

            var result = Convert(capture, out var entries, out _);

            Assert.That(result.Errors, Is.Empty);
            Assert.That(entries.Select(Describe), Is.EqualTo(new[]
            {
                "0 CONNECTED", "3 C>S 91", "11 S>C B9", "11 S>C 73", "20 CLOSED Cancelled",
            }));
            Assert.That(entries[1].IsMasked, Is.True);
            Assert.That(entries[2].Packet, Is.EqualTo(features));
            Assert.That(entries[3].Packet, Is.EqualTo(ping));
        }

        [Test]
        public void TwoConnections_Interleaved_EachKeepsItsOwnState()
        {
            var capture = new CaptureBuilder()
                .Open(0, 1)
                .C2S(1, 1, Seed())
                .Open(2, 2)
                .C2S(3, 2, KeyBytes().Concat(ClientLoginSamples.GameLogin(Key)).ToArray())
                .S2C(4, 1, Redirect())
                .S2C(5, 2, HuffmanTestEncoder.Compress(new byte[] { 0x73, 0x01 }));

            var result = Convert(capture, out var entries, out _);

            Assert.That(result.Errors, Is.Empty);
            Assert.That(entries.Select(Describe), Is.EqualTo(new[]
            {
                "0 CONNECTED", "1 C>S EF", "2 CONNECTED", "3 C>S 91", "4 S>C 8C", "5 S>C 73",
            }));
        }

        [Test]
        public void UnknownClientPacket_ErrorWithPlace_EarlierPacketsKept()
        {
            var capture = new CaptureBuilder()
                .Open(0, 1)
                .C2S(1, 1, Seed().Concat(new byte[] { 0xFE, 0x01, 0x02 }).ToArray());

            var result = Convert(capture, out var entries, out _);

            Assert.That(entries.Select(Describe), Is.EqualTo(new[] { "0 CONNECTED", "1 C>S EF" }));
            Assert.That(result.Errors.Count, Is.EqualTo(1));
            Assert.That(result.Errors[0], Does.Contain("соединение 1").And.Contain("C>S").And.Contain("0xFE"));
        }

        [Test]
        public void BrokenCaptureLine_ErrorWithLineNumber()
        {
            var capture = new CaptureBuilder().Open(0, 1).Raw("12\t1\tc2s\tzz");

            var result = Convert(capture, out _, out _);

            Assert.That(result.Errors.Single(), Does.Contain("строка 3"));
        }

        [Test]
        public void NotACapture_Error()
        {
            var result = CaptureConverter.Convert(new StringReader("hello"), new StringWriter(), "test");

            Assert.That(result.Errors.Single(), Does.Contain("не запись посредника"));
        }

        private static CaptureResult Convert(CaptureBuilder capture, out List<PacketLogEntry> entries, out string text)
        {
            var output = new StringWriter();
            var result = CaptureConverter.Convert(new StringReader(capture.Text), output, "test");
            text = output.ToString();
            entries = new List<PacketLogEntry>();
            var reader = new PacketLogReader(new StringReader(text));
            while (reader.TryRead(out var entry))
            {
                entries.Add(entry);
            }

            return result;
        }

        private static string Describe(PacketLogEntry e) => e.Kind switch
        {
            PacketLogKind.Connected => e.TimeMs + " CONNECTED",
            PacketLogKind.Closed => e.TimeMs + " CLOSED " + e.CloseReason,
            _ => e.TimeMs + " " + (e.Direction == PacketDirection.ClientToServer ? "C>S" : "S>C") + " " + e.Packet[0].ToString("X2"),
        };

        /// <summary>Ключ перед 0x91 — те же 4 байта, что в поле ключа 0x91 и в 0x8C (big-endian).</summary>
        private static byte[] KeyBytes() => new byte[] { 0xE6, 0xA2, 0x3F, 0x31 };

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", " ");

        /// <summary>0xEF LoginSeed клиента 7.0.95.0: seed, версия major, minor, revision, prototype (IncomingAccountPackets.cs). 21 байт.</summary>
        private static byte[] Seed() => new byte[]
        {
            0xEF, 0x01, 0x02, 0x03, 0x04, 0, 0, 0, 7, 0, 0, 0, 0, 0, 0, 0, 95, 0, 0, 0, 0,
        };

        /// <summary>0xA8 ServerList с одним шардом (OutgoingAccountPackets.cs, SendAccountLoginAck). 46 байт.</summary>
        private static byte[] ServerList()
        {
            var p = new byte[46];
            p[0] = 0xA8;
            p[2] = 46;
            p[3] = 0x5D;
            p[5] = 1;
            return p;
        }

        /// <summary>0x8C ServerRedirect: адрес, порт, ключ (OutgoingAccountPackets.cs, SendPlayServerAck). 11 байт.</summary>
        private static byte[] Redirect() => new byte[] { 0x8C, 0x7F, 0x00, 0x00, 0x01, 0x0A, 0x21, 0xE6, 0xA2, 0x3F, 0x31 };

        /// <summary>Строки записи посредника: «мс, соединение, событие[, байты hex]».</summary>
        private sealed class CaptureBuilder
        {
            private readonly StringBuilder _text = new StringBuilder(CaptureConverter.FormatLine + "\n");

            public string Text => _text.ToString();

            public CaptureBuilder Open(long ms, int connection) => Raw(ms + "\t" + connection + "\topen");

            public CaptureBuilder C2S(long ms, int connection, byte[] data) => Raw(ms + "\t" + connection + "\tc2s\t" + ToHex(data));

            public CaptureBuilder S2C(long ms, int connection, byte[] data) => Raw(ms + "\t" + connection + "\ts2c\t" + ToHex(data));

            public CaptureBuilder Close(long ms, int connection, string by) => Raw(ms + "\t" + connection + "\tclose\t" + by);

            public CaptureBuilder Raw(string line)
            {
                _text.Append(line).Append('\n');
                return this;
            }

            private static string ToHex(byte[] data) => BitConverter.ToString(data).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
