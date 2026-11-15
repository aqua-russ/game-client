using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TheVerge.Core.Primitives;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Framing;
using TheVerge.Network.Packets.Movement;
using TheVerge.Network.Session;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Network.Packets.Movement
{
    /// <summary>
    /// Запросы клиента о движении — как читает сервер. 0x02 (Projects/UOContent/Network/Packets/IncomingMovementPackets.cs,
    /// MovementReq): byte направление (бит 0x80 — бег, Direction.Running), byte номер шага, uint ключ fastwalk (сервер не
    /// использует, шлём 0); всего 7 байт. 0x22 от клиента (IncomingPlayerPackets.cs, Resynchronize; Register(0x22, 3)):
    /// сервер полей не читает. Байты шага сверены с эталоном ClassicUO golden/02-walk-20.tsv (docs/REFERENCE-CLIENT.md):
    /// поворот на север из направления Up номером 0, дальше шаги на север 1, 2, … пешком.
    /// </summary>
    public sealed class MovementRequestTests
    {
        private static readonly string Golden = Path.Combine(AppContext.BaseDirectory, "Golden", "02-walk-20.tsv");

        private NetworkSession _session = null!;
        private MovementSender _sender = null!;

        [SetUp]
        public void SetUp()
        {
            _session = new NetworkSession(new PacketDispatcher(new RecordingLog()), new RecordingLog());
            _sender = new MovementSender(_session);
        }

        [Test]
        public void Walk_SameBytesAsClassicUoGolden()
        {
            var golden = ReadGoldenMoveRequests();
            Assert.That(golden.Count, Is.EqualTo(28), "в эталоне поворот и 27 шагов");

            for (int i = 0; i < golden.Count; i++)
            {
                _sender.SendStep(Direction.North, false, (byte)i);
            }

            Assert.That(_session.Outgoing.Count, Is.EqualTo(golden.Count));
            for (int i = 0; i < golden.Count; i++)
            {
                Assert.That(_session.Outgoing.PacketAt(i).ToArray(), Is.EqualTo(golden[i]), "запрос " + i);
            }
        }

        [Test]
        public void Run_SetsRunningBit()
        {
            _sender.SendStep(Direction.East, true, 5);

            Assert.That(_session.Outgoing.Pending.ToArray(), Is.EqualTo(new byte[] { 0x02, 0x82, 0x05, 0x00, 0x00, 0x00, 0x00 }));
        }

        [Test]
        public void Walk_RunningBitInDirectionIsIgnored()
        {
            _sender.SendStep(Direction.Up | Direction.Running, false, 255);

            Assert.That(_session.Outgoing.Pending.ToArray(), Is.EqualTo(new byte[] { 0x02, 0x07, 0xFF, 0x00, 0x00, 0x00, 0x00 }),
                "бег задаёт только флаг run");
        }

        [Test]
        public void Resync_ThreeBytes()
        {
            _sender.SendResync();

            var packet = _session.Outgoing.Pending.ToArray();
            Assert.That(packet, Is.EqualTo(new byte[] { 0x22, 0x00, 0x00 }));
            Assert.That(PacketTable.Get(PacketDirection.ClientToServer, 0x22).FixedLength, Is.EqualTo(packet.Length));
        }

        private static List<byte[]> ReadGoldenMoveRequests()
        {
            var result = new List<byte[]>();
            using (var file = new StreamReader(Golden))
            {
                var reader = new PacketLogReader(file);
                while (reader.TryRead(out var entry))
                {
                    if (entry.Kind == PacketLogKind.Packet && entry.Direction == PacketDirection.ClientToServer
                        && entry.Packet.Length > 0 && entry.Packet[0] == MoveRequestPacket.PacketId)
                    {
                        result.Add(entry.Packet);
                    }
                }
            }

            return result;
        }
    }
}
