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
    /// Список шардов 0xA8, выбор 0xA0 и редирект 0x8C — байты по коду сервера (AccountServerSamples;
    /// OutgoingAccountPackets.cs, SendAccountLoginAck и SendPlayServerAck; IncomingAccountPackets.cs, PlayServer).
    /// Главная ловушка: в 0xA8 IP идёт задом наперёд, в 0x8C — в обычном порядке (ServerInfo.RawAddress).
    /// </summary>
    public sealed class ShardPacketTests
    {
        private static readonly byte[] Localhost = { 127, 0, 0, 1 };
        private static readonly byte[] LanAddress = { 192, 168, 1, 20 };

        [Test]
        public void ServerList_IpReversedInPacket_ReadInNormalOrder()
        {
            var packet = AccountServerSamples.ServerList(("Remaster UO", 0, 3, Localhost), ("Lan", 0, 3, LanAddress));
            Assert.That(packet[6 + 36], Is.EqualTo(0x01), "проверка самого образца: в 0xA8 IP задом наперёд");

            var flow = Dispatch(packet);

            Assert.That(flow.Shards[0].Address, Is.EqualTo("127.0.0.1"));
            Assert.That(flow.Shards[1].Address, Is.EqualTo("192.168.1.20"));
        }

        [Test]
        public void ServerList_NamePercentTimeZoneIndex_AsServerWrote()
        {
            var flow = Dispatch(AccountServerSamples.ServerList(("First", 0, 3, Localhost), ("Ünïcode-1", 85, -5, LanAddress)));

            Assert.That(flow.State, Is.EqualTo(LoginState.ShardSelection));
            Assert.That(flow.Shards.Count, Is.EqualTo(2));
            Assert.That(flow.Shards[1].Index, Is.EqualTo(1));
            Assert.That(flow.Shards[1].Name, Is.EqualTo("Ünïcode-1"));
            Assert.That(flow.Shards[1].FullPercent, Is.EqualTo(85));
            Assert.That(flow.Shards[1].TimeZone, Is.EqualTo(-5));
        }

        [Test]
        public void ServerList_NameOfFull32Bytes_Read()
        {
            var name = new string('N', 32);

            var flow = Dispatch(AccountServerSamples.ServerList((name, 0, 0, Localhost)));

            Assert.That(flow.Shards[0].Name, Is.EqualTo(name));
        }

        [Test]
        public void ServerList_CountDoesNotMatchLength_ProtocolError()
        {
            var packet = AccountServerSamples.ServerList(("First", 0, 3, Localhost));
            packet[5] = 2; // число шардов 2, а места — на один

            var flow = Dispatch(packet);

            Assert.That(flow.State, Is.EqualTo(LoginState.Failed));
            Assert.That(flow.Error.Kind, Is.EqualTo(LoginErrorKind.ProtocolError));
            Assert.That(flow.Error.Detail, Does.Contain("0xA8"));
        }

        [Test]
        public void ServerList_OnlyHeader_ProtocolErrorNotSkip()
        {
            var flow = Dispatch(new byte[] { 0xA8, 0x00, 0x03 });

            Assert.That(flow.Error.Kind, Is.EqualTo(LoginErrorKind.ProtocolError));
        }

        [Test]
        public void SelectServer_IndexBigEndian_3Bytes()
        {
            var bytes = PacketBytes.Write((ref PacketWriter w) => SelectServerPacket.Write(ref w, 0x0102));

            Assert.That(bytes, Is.EqualTo(new byte[] { 0xA0, 0x01, 0x02 }));
            Assert.That(bytes.Length, Is.EqualTo(PacketTable.Get(PacketDirection.ClientToServer, 0xA0).FixedLength));
        }

        [Test]
        public void Redirect_IpNormalOrder_PortAndKeyBigEndian()
        {
            var flow = AwaitingRedirect();

            Dispatch(flow, AccountServerSamples.PlayServerAck(Localhost, 2593, unchecked((int)0xDEADBEEF)));

            Assert.That(flow.Redirect.Host, Is.EqualTo("127.0.0.1"));
            Assert.That(flow.Redirect.Port, Is.EqualTo(2593));
            Assert.That(flow.Redirect.AuthKey, Is.EqualTo(unchecked((int)0xDEADBEEF)));
        }

        [Test]
        public void Redirect_PortAbove32767_ReadUnsigned()
        {
            // Сервер пишет порт как short: 40000 приходит отрицательным числом, но это порт 40000.
            var flow = AwaitingRedirect();

            Dispatch(flow, AccountServerSamples.PlayServerAck(LanAddress, 40000, 5));

            Assert.That(flow.Redirect.Host, Is.EqualTo("192.168.1.20"));
            Assert.That(flow.Redirect.Port, Is.EqualTo(40000));
        }

        private static LoginFlow AwaitingRedirect()
        {
            var flow = Dispatch(AccountServerSamples.ServerList(("First", 0, 3, Localhost)));
            flow.SelectShard(0);
            return flow;
        }

        private static LoginFlow Dispatch(byte[] packet)
        {
            var flow = new LoginFlow(new RecordingAccountSender(), new RecordingConnector(), 1);
            flow.Start("127.0.0.1", 2593, new LoginCredentials("player", "secret"));
            flow.OnConnected();
            Dispatch(flow, packet);
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
