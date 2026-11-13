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
    /// 0xB9, 0xA9 и 0x5D — байты по коду сервера (AccountServerSamples; OutgoingAccountPackets.cs, SendSupportedFeature и
    /// SendCharacterList; IncomingAccountPackets.cs, PlayCharacter). Имена персонажей — UTF-8 (наше расширение E1).
    /// </summary>
    public sealed class CharacterListPacketTests
    {
        private static readonly AccountServerSamples.City[] TwoCities =
        {
            new AccountServerSamples.City("Britain", "The Wayfarer's Inn", 1602, 1591, 20, 1075074),
            new AccountServerSamples.City("Yew", "The Empath Abbey", 633, 858, 0, 1075072),
        };

        [Test]
        public void SupportedFeatures_UInt32Flags()
        {
            var flow = GameLoginSent();

            Dispatch(flow, AccountServerSamples.SupportedFeatures(0x8000_02DF));

            Assert.That(flow.Characters.SupportedFeatures, Is.EqualTo(0x8000_02DFu));
        }

        [Test]
        public void CharacterList_SlotsNamesUtf8_EmptySlots()
        {
            var flow = GameLoginSent();

            Dispatch(flow, AccountServerSamples.CharacterList(new[] { "AQua", null, "Борис", null, null }, TwoCities, 0x14));

            Assert.That(flow.State, Is.EqualTo(LoginState.CharacterSelection));
            var slots = flow.Characters.Slots;
            Assert.That(slots.Count, Is.EqualTo(5));
            Assert.That(slots[0].Name, Is.EqualTo("AQua"));
            Assert.That(slots[1].IsEmpty, Is.True);
            Assert.That(slots[2].Name, Is.EqualTo("Борис"));
            Assert.That(slots[2].Slot, Is.EqualTo(2));
            Assert.That(flow.Characters.ListFlags, Is.EqualTo(0x14));
        }

        [Test]
        public void CharacterList_CitiesNewFormat()
        {
            var flow = GameLoginSent();

            Dispatch(flow, AccountServerSamples.CharacterList(new[] { "AQua", null, null, null, null }, TwoCities, 0));

            var cities = flow.Characters.Cities;
            Assert.That(cities.Count, Is.EqualTo(2));
            Assert.That(cities[1].Index, Is.EqualTo(1));
            Assert.That(cities[1].City, Is.EqualTo("Yew"));
            Assert.That(cities[1].Building, Is.EqualTo("The Empath Abbey"));
            Assert.That(cities[0].Location.X, Is.EqualTo(1602));
            Assert.That(cities[0].Location.Y, Is.EqualTo(1591));
            Assert.That(cities[0].Location.Z, Is.EqualTo(20));
            Assert.That(cities[0].MapId, Is.EqualTo(0));
            Assert.That(cities[0].Description, Is.EqualTo(1075074));
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void CharacterList_LengthDoesNotMatchCounts_ProtocolError(int extraCity)
        {
            var packet = AccountServerSamples.CharacterList(new[] { "AQua", null, null, null, null }, TwoCities, 0);
            packet[4 + 5 * 60] = (byte)(2 + extraCity); // число городов не сходится с длиной

            var flow = GameLoginSent();
            Dispatch(flow, packet);

            Assert.That(flow.Error.Kind, Is.EqualTo(LoginErrorKind.ProtocolError));
            Assert.That(flow.Error.Detail, Does.Contain("0xA9"));
        }

        [Test]
        public void CharacterList_SlotCountBeyondLength_ProtocolError()
        {
            var flow = GameLoginSent();

            Dispatch(flow, new byte[] { 0xA9, 0x00, 0x05, 0x07, 0x00 });

            Assert.That(flow.Error.Kind, Is.EqualTo(LoginErrorKind.ProtocolError));
        }

        [Test]
        public void PopupMessage_CharInWorld_CharacterRejected()
        {
            var flow = GameLoginSent();
            Dispatch(flow, AccountServerSamples.CharacterList(new[] { "AQua", null, null, null, null }, TwoCities, 0));
            flow.Characters.Select(0);

            Dispatch(flow, new byte[] { 0x53, 0x05 }); // SendPopupMessage(PMMessage.CharInWorld)

            Assert.That(flow.Error.Kind, Is.EqualTo(LoginErrorKind.CharacterRejected));
            Assert.That((CharacterRejectReason)flow.Error.Code, Is.EqualTo(CharacterRejectReason.CharInWorld));
        }

        [Test]
        public void PlayCharacter_73Bytes_SlotAndFeluccaFlag_WhereServerReads()
        {
            var bytes = PacketBytes.Write((ref PacketWriter w) => PlayCharacterPacket.Write(ref w, 3, "Борис"));

            Assert.That(bytes.Length, Is.EqualTo(PacketTable.Get(PacketDirection.ClientToServer, 0x5D).FixedLength));
            Assert.That(bytes[0], Is.EqualTo(0x5D));
            var reader = new PacketReader(bytes.AsSpan(1));
            Assert.That(reader.ReadUInt32(), Is.EqualTo(0xEDEDEDEDu));
            Assert.That(reader.ReadUtf8(30), Is.EqualTo("Борис"));
            reader.Skip(2);
            Assert.That(reader.ReadInt32(), Is.EqualTo(0x01), "флаги карт клиента: только Фелукка (ClientFlags.Felucca)");
            reader.Skip(24);
            Assert.That(reader.ReadInt32(), Is.EqualTo(3), "слот");
            Assert.That(reader.ReadInt32(), Is.EqualTo(0), "IP клиента сервер не читает");
        }

        private static LoginFlow GameLoginSent()
        {
            var flow = new LoginFlow(new RecordingAccountSender(), new RecordingConnector(), 1);
            flow.Start("127.0.0.1", 2593, new LoginCredentials("player", "secret"));
            flow.OnConnected();
            flow.OnShardList(new[] { new ShardEntry(0, "Shard", 0, 0, "127.0.0.1") });
            flow.SelectShard(0);
            flow.OnRedirect(new ServerRedirect("127.0.0.1", 2593, 5));
            flow.OnConnected();
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
