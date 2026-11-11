using System.Linq;
using NUnit.Framework;
using TheVerge.Network.Framing;

namespace TheVerge.Tests.Network.Framing
{
    /// <summary>
    /// Где у пакета подкоманда (PLAN Стадия 1, п. 2; docs/protocol/packets.md). Ожидания — по серверу:
    /// 0xBF — 2 байта сразу после длины (IncomingExtendedCommandPackets.cs, ExtendedCommand: ReadUInt16;
    /// OutgoingMapPackets.cs, SendMapChange); 0xD7 — 2 байта после serial (IncomingPlayerPackets.cs, EncodedCommand);
    /// 0x12 — 1 байт после длины (IncomingPlayerPackets.cs, TextCommand); 0xF0 и 0xF1 — 1 байт после длины
    /// (UOContent/Network/ProtocolExtensions.cs: ReadByte; MapUO.cs, SendPartyMemberLocations).
    /// </summary>
    public sealed class SubCommandLayoutTests
    {
        [TestCase(PacketDirection.ServerToClient, 0xBF, 3, 2)]
        [TestCase(PacketDirection.ServerToClient, 0xF0, 3, 1)]
        [TestCase(PacketDirection.ClientToServer, 0xBF, 3, 2)]
        [TestCase(PacketDirection.ClientToServer, 0xD7, 7, 2)]
        [TestCase(PacketDirection.ClientToServer, 0x12, 3, 1)]
        [TestCase(PacketDirection.ClientToServer, 0xF0, 3, 1)]
        [TestCase(PacketDirection.ClientToServer, 0xF1, 3, 1)]
        public void GetSubCommandLayout_PacketWithSubCommands_AsServer(PacketDirection direction, int id, int offset, int size)
        {
            var layout = PacketTable.GetSubCommandLayout(direction, (byte)id);

            Assert.That(layout.HasSubCommand, Is.True);
            Assert.That(layout.Offset, Is.EqualTo(offset));
            Assert.That(layout.Size, Is.EqualTo(size));
        }

        [TestCase(PacketDirection.ClientToServer)]
        [TestCase(PacketDirection.ServerToClient)]
        public void GetSubCommandLayout_EveryId_HasLayoutExactlyWhenMatrixHasSubCommands(PacketDirection direction)
        {
            for (var id = 0; id <= 0xFF; id++)
            {
                var inMatrix = PacketMatrix.Rows.Any(r => r.Direction == direction && r.Id == id && r.SubCommand.HasValue);

                var layout = PacketTable.GetSubCommandLayout(direction, (byte)id);

                Assert.That(layout.HasSubCommand, Is.EqualTo(inMatrix), "0x" + id.ToString("X2") + " " + direction);
            }
        }

        [Test]
        public void GetSubCommandLayout_NoSubCommand_IsEmpty()
        {
            var layout = PacketTable.GetSubCommandLayout(PacketDirection.ServerToClient, 0x1B);

            Assert.That(layout.HasSubCommand, Is.False);
            Assert.That(layout.Size, Is.EqualTo(0));
        }
    }
}
