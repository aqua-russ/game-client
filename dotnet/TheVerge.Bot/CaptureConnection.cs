using System;
using System.Collections.Generic;
using TheVerge.Network.Compression;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Framing;
using TheVerge.Network.Session;

namespace TheVerge.Bot
{
    /// <summary>
    /// Одно соединение записи посредника: нарезка пакетов в обе стороны. Вид соединения — по первым 9 байтам клиента:
    /// 4 байта ключа, 0x91 (GameLogin) и в нём тот же ключ — игровое соединение: ключ не пакет (пропускается), всё от
    /// сервера сжато Huffman (IncomingAccountPackets.cs, GameLogin: CompressionEnabled). Иначе — вход: с 0xEF (LoginSeed)
    /// всё пакеты, а без него первые 4 байта — seed старых клиентов; ответы сервера не сжаты. После ошибки нарезки
    /// направление дальше не режется — как у клиента (AGENTS.md §3.5).
    /// </summary>
    internal sealed class CaptureConnection
    {
        private const byte LoginSeed = 0xEF;
        private const byte GameLogin = 0x91;
        private const int KeyLength = 4;
        private const int HeadLength = KeyLength + 1 + KeyLength;

        private readonly int _id;
        private readonly PacketLogWriter _writer;
        private readonly CaptureResult _result;
        private readonly PacketFramer _client = new PacketFramer(PacketDirection.ClientToServer);
        private readonly PacketFramer _server = new PacketFramer(PacketDirection.ServerToClient);
        private readonly HuffmanDecoder _decoder = new HuffmanDecoder();
        private readonly byte[] _decoded = new byte[PacketInfo.MaxLength];
        private readonly List<byte> _head = new List<byte>();
        private bool? _compressed;

        public CaptureConnection(int id, PacketLogWriter writer, CaptureResult result)
        {
            _id = id;
            _writer = writer;
            _result = result;
        }

        public void FromClient(byte[] data)
        {
            if (_compressed != null)
            {
                Feed(_client, data);
                return;
            }

            _head.AddRange(data);
            if (_head.Count < HeadLength)
            {
                return;
            }

            _compressed = IsGameLogin();
            var start = _compressed == true || _head[0] != LoginSeed ? KeyLength : 0;
            Feed(_client, _head.GetRange(start, _head.Count - start).ToArray());
            _head.Clear();
        }

        public void FromServer(byte[] data)
        {
            if (_compressed == null)
            {
                _result.AddError("соединение " + _id + ": сервер прислал данные раньше клиента — вид соединения неизвестен.");
                _compressed = false;
            }

            if (_compressed == false)
            {
                Feed(_server, data);
                return;
            }

            ReadOnlySpan<byte> rest = data;
            while (!rest.IsEmpty)
            {
                var written = _decoder.Decode(rest, _decoded, out var consumed);
                Feed(_server, _decoded.AsSpan(0, written));
                rest = rest.Slice(consumed);
            }
        }

        private bool IsGameLogin()
        {
            if (_head[KeyLength] != GameLogin)
            {
                return false;
            }

            for (var i = 0; i < KeyLength; i++)
            {
                if (_head[i] != _head[KeyLength + 1 + i])
                {
                    return false;
                }
            }

            return true;
        }

        private void Feed(PacketFramer framer, ReadOnlySpan<byte> data)
        {
            while (!framer.IsFailed)
            {
                var accepted = framer.Append(data);
                data = data.Slice(accepted);
                if (!Drain(framer) || data.IsEmpty)
                {
                    return;
                }
            }
        }

        private bool Drain(PacketFramer framer)
        {
            var direction = framer == _client ? PacketDirection.ClientToServer : PacketDirection.ServerToClient;
            FrameStatus status;
            while ((status = framer.Next(out var packet)) == FrameStatus.Packet)
            {
                _writer.Packet(direction, packet, true);
                if (direction == PacketDirection.ClientToServer)
                {
                    _result.ClientPackets++;
                }
                else
                {
                    _result.ServerPackets++;
                }
            }

            if (status == FrameStatus.Error)
            {
                var error = framer.Error;
                _result.AddError("соединение " + _id + ", " + (direction == PacketDirection.ClientToServer ? "C>S" : "S>C") +
                                 ": " + error.Kind + ", пакет 0x" + error.Id.ToString("X2") + ", дамп " + PacketDump.ToHex(error.Dump) + ".");
                return false;
            }

            return true;
        }
    }
}
