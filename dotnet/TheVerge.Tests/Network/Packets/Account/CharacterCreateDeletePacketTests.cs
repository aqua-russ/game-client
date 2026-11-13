using System;
using NUnit.Framework;
using TheVerge.Core.Session;
using TheVerge.Network.Framing;
using TheVerge.Network.IO;
using TheVerge.Network.Packets.Account;
using TheVerge.Network.Session;
using TheVerge.Tests.Core.Session;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Network.Packets.Account
{
    /// <summary>
    /// 0xF8, 0x83, 0x85, 0x86 — по коду сервера: что и в каком порядке читает CreateCharacter и DeleteCharacter
    /// (IncomingAccountPackets.cs), что пишут SendCharacterDeleteResult и SendCharacterListUpdate (OutgoingAccountPackets.cs).
    /// </summary>
    public sealed class CharacterCreateDeletePacketTests
    {
        [Test]
        public void CreateCharacter_ServerReadsEveryFieldWhereWritten_106Bytes()
        {
            var c = new NewCharacter
            {
                Name = "Борис", Female = true, Strength = 50, Dexterity = 30, Intelligence = 10,
                Skills = new[] { (25, 50), (26, 30), (16, 20), (0, 0) }, CityIndex = 2,
                SkinHue = 0x83EA, HairStyle = 0x203B, HairHue = 0x044E, BeardStyle = 0x2040, BeardHue = 0x0455,
                ShirtHue = 0x0003, PantsHue = 0x0004,
            };

            var bytes = PacketBytes.Write((ref PacketWriter w) => CreateCharacterPacket.Write(ref w, c, 1));

            Assert.That(bytes.Length, Is.EqualTo(PacketTable.Get(PacketDirection.ClientToServer, 0xF8).FixedLength));
            Assert.That(bytes[0], Is.EqualTo(0xF8));
            var r = new PacketReader(bytes.AsSpan(1));
            r.Skip(9);
            Assert.That(r.ReadUtf8(30), Is.EqualTo("Борис"));
            r.Skip(2);
            Assert.That(r.ReadInt32(), Is.EqualTo(0x01), "флаги карт: только Фелукка");
            r.Skip(8);
            Assert.That(r.ReadByte(), Is.EqualTo(0), "профессия: своя");
            r.Skip(15);
            Assert.That(r.ReadByte() % 2, Is.EqualTo(1), "нечётный байт пола — женщина");
            Assert.That(new[] { r.ReadByte(), r.ReadByte(), r.ReadByte() }, Is.EqualTo(new byte[] { 50, 30, 10 }));
            Assert.That(new[] { r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte() },
                Is.EqualTo(new byte[] { 25, 50, 26, 30, 16, 20, 0, 0 }));
            Assert.That(r.ReadUInt16(), Is.EqualTo(0x83EA));
            Assert.That(new[] { r.ReadInt16(), r.ReadInt16(), r.ReadInt16(), r.ReadInt16() }, Is.EqualTo(new short[] { 0x203B, 0x044E, 0x2040, 0x0455 }));
            r.ReadByte();
            Assert.That(r.ReadByte(), Is.EqualTo(2), "город");
            r.Skip(8);
            Assert.That(r.ReadInt16(), Is.EqualTo(3));
            Assert.That(r.ReadInt16(), Is.EqualTo(4));
            Assert.That(r.Remaining, Is.EqualTo(0));
        }

        [Test]
        public void CreateCharacter_Male_HumanRaceByte()
        {
            var c = new NewCharacter { Name = "A", Skills = new (int, int)[4] };

            var bytes = PacketBytes.Write((ref PacketWriter w) => CreateCharacterPacket.Write(ref w, c, 0));

            // Сервер для клиентов 7.0+ (StygianAbyss): раса = байт < 4 ? человек : …; пол — чётность.
            Assert.That(bytes[1 + 9 + 30 + 2 + 4 + 8 + 1 + 15], Is.EqualTo(2));
        }

        [Test]
        public void DeleteCharacter_SlotAfter30Bytes_39Bytes()
        {
            var bytes = PacketBytes.Write((ref PacketWriter w) => DeleteCharacterPacket.Write(ref w, 4));

            Assert.That(bytes.Length, Is.EqualTo(PacketTable.Get(PacketDirection.ClientToServer, 0x83).FixedLength));
            var r = new PacketReader(bytes.AsSpan(1));
            r.Skip(30);
            Assert.That(r.ReadInt32(), Is.EqualTo(4));
        }

        [Test]
        public void DeleteResult_ThenListUpdate_ReasonAndNewSlots()
        {
            var flow = Selecting();
            CharacterDeleteResult? reason = null;
            flow.Characters.DeleteRejected += r => reason = r;

            Dispatch(flow, new byte[] { 0x85, 0x03 });
            Dispatch(flow, ListUpdate("AQua", null, null, null, null));

            Assert.That(reason, Is.EqualTo(CharacterDeleteResult.CharTooYoung));
            Assert.That(flow.Characters.Slots.Count, Is.EqualTo(5));
            Assert.That(flow.Characters.Slots[0].Name, Is.EqualTo("AQua"));
            Assert.That(flow.Characters.Slots[1].IsEmpty, Is.True);
        }

        [Test]
        public void ListUpdate_LengthMismatch_ProtocolError()
        {
            var flow = Selecting();
            var packet = ListUpdate("AQua", null, null, null, null);
            packet[3] = 6;

            Dispatch(flow, packet);

            Assert.That(flow.Error.Kind, Is.EqualTo(LoginErrorKind.ProtocolError));
        }

        // SendCharacterListUpdate: 0x86, ushort длина, byte число, на слот имя UTF-8 30 + 30 нулей.
        private static byte[] ListUpdate(params string?[] names)
        {
            var packet = new byte[4 + names.Length * 60];
            packet[0] = 0x86;
            packet[1] = (byte)(packet.Length >> 8);
            packet[2] = (byte)packet.Length;
            packet[3] = (byte)names.Length;
            for (var i = 0; i < names.Length; i++)
            {
                if (names[i] != null)
                {
                    System.Text.Encoding.UTF8.GetBytes(names[i]!).CopyTo(packet, 4 + i * 60);
                }
            }

            return packet;
        }

        private static LoginFlow Selecting()
        {
            var flow = new LoginFlow(new RecordingAccountSender(), new RecordingConnector(), 1);
            flow.Start("127.0.0.1", 2593, new LoginCredentials("player", "secret"));
            flow.OnConnected();
            flow.OnShardList(new[] { new ShardEntry(0, "Shard", 0, 0, "127.0.0.1") });
            flow.SelectShard(0);
            flow.OnRedirect(new ServerRedirect("127.0.0.1", 2593, 5));
            flow.OnConnected();
            flow.Characters.OnCharacterList(new[] { new CharacterSlot(0, "AQua"), new CharacterSlot(1, "Old") }, Array.Empty<StartingCity>(), 0);
            return flow;
        }

        private static void Dispatch(LoginFlow flow, byte[] packet)
        {
            var dispatcher = new PacketDispatcher(new RecordingLog());
            AccountHandlers.Register(dispatcher, flow);
            Assert.That(dispatcher.Dispatch(packet), Is.EqualTo(DispatchResult.Handled));
        }
    }
}
