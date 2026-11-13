using System;
using NUnit.Framework;
using TheVerge.Core.Primitives;
using TheVerge.Core.Targeting;
using TheVerge.Network.Framing;
using TheVerge.Network.IO;
using TheVerge.Network.Packets.Targeting;
using TheVerge.Network.Session;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Network.Packets.Targeting
{
    /// <summary>
    /// 0x6C в обе стороны. От сервера — OutgoingTargetPackets.cs: SendTargetReq (bool AllowGround, int TargetID, byte Flags,
    /// 12 нулей) и SendCancelTarget (то же, флаг 3). От клиента — как читает IncomingTargetingPackets.cs, TargetResponse:
    /// byte тип, int номер курсора, byte флаги, uint серийник, short X, Y, byte (пропускается), sbyte Z, ushort графика.
    /// </summary>
    public sealed class TargetPacketTests
    {
        [Test]
        public void Request_OpensCursor()
        {
            var cursor = new TargetCursor(new TargetSender(Session()));
            var dispatcher = new PacketDispatcher(new RecordingLog());
            TargetHandlers.Register(dispatcher, cursor);

            dispatcher.Dispatch(new byte[] { 0x6C, 0x01, 0x00, 0x00, 0x12, 0x34, 0x01, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });

            Assert.That(cursor.IsActive, Is.True);
            Assert.That(cursor.AllowsLocation, Is.True);
            Assert.That(cursor.CursorId, Is.EqualTo(0x1234));
            Assert.That(cursor.Flags, Is.EqualTo(TargetCursor.FlagHarmful));
        }

        [Test]
        public void ServerCancel_ClosesCursor()
        {
            var cursor = new TargetCursor(new TargetSender(Session()));
            var dispatcher = new PacketDispatcher(new RecordingLog());
            TargetHandlers.Register(dispatcher, cursor);
            cursor.OnRequest(true, 5, 0);

            dispatcher.Dispatch(new byte[] { 0x6C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });

            Assert.That(cursor.IsActive, Is.False);
        }

        [Test]
        public void Response_Location_AsServerReads()
        {
            var response = new TargetResponse(TargetKind.Location, 0x1234, 0x01, Serial.Zero, new Point3D(1500, 1600, -5), 0x0EED);

            var bytes = PacketBytes.Write((ref PacketWriter w) => TargetResponsePacket.Write(ref w, response));

            Assert.That(bytes, Is.EqualTo(new byte[]
            {
                0x6C, 0x01, 0x00, 0x00, 0x12, 0x34, 0x01, 0x00, 0x00, 0x00, 0x00, 0x05, 0xDC, 0x06, 0x40, 0x00, 0xFB, 0x0E, 0xED
            }));
            Assert.That(bytes.Length, Is.EqualTo(PacketTable.Get(PacketDirection.ClientToServer, 0x6C).FixedLength));
        }

        [Test]
        public void Response_Object_AsServerReads()
        {
            var response = new TargetResponse(TargetKind.Object, 7, 0, new Serial(0x40000500), Point3D.Zero, 0);

            var bytes = PacketBytes.Write((ref PacketWriter w) => TargetResponsePacket.Write(ref w, response));

            var reader = new PacketReader(bytes.AsSpan(1));
            Assert.That(reader.ReadByte(), Is.EqualTo(0), "тип: объект");
            Assert.That(reader.ReadInt32(), Is.EqualTo(7));
            reader.ReadByte();
            Assert.That(reader.ReadUInt32(), Is.EqualTo(0x40000500u));
        }

        [Test]
        public void Response_Cancel_MinusOne()
        {
            var cursor = new TargetCursor(new TargetSender(Session(out var session)));
            cursor.OnRequest(true, 9, 0);

            cursor.Cancel();

            var reader = new PacketReader(session.Outgoing.Pending.ToArray().AsSpan(1));
            reader.ReadByte();
            Assert.That(reader.ReadInt32(), Is.EqualTo(9));
            reader.ReadByte();
            Assert.That(reader.ReadUInt32(), Is.EqualTo(0u), "серийник 0");
            Assert.That(reader.ReadInt16(), Is.EqualTo(-1));
            Assert.That(reader.ReadInt16(), Is.EqualTo(-1));
        }

        private static NetworkSession Session() => Session(out _);

        private static NetworkSession Session(out NetworkSession session)
        {
            session = new NetworkSession(new PacketDispatcher(new RecordingLog()), new RecordingLog());
            return session;
        }
    }
}
