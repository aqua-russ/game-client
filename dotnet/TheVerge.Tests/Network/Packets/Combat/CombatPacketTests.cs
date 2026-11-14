using NUnit.Framework;
using TheVerge.Core.Combat;
using TheVerge.Core.Primitives;
using TheVerge.Network.Packets.Combat;
using TheVerge.Network.Session;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Network.Packets.Combat
{
    /// <summary>
    /// Стойка и атака. От сервера (Projects/Server/Network/Packets/OutgoingCombatPackets.cs): 0x72 SendSetWarMode —
    /// bool, 0x00, 0x32, 0x00; 0xAA SendChangeCombatant — Serial (0 — противника нет). От клиента — как читает сервер
    /// (Projects/UOContent/Network/Packets/IncomingPlayerPackets.cs): 0x72 SetWarMode — bool, дальше не читает (5 байт;
    /// хвост пишем, как сервер свой ответ); 0x05 AttackReq — uint серийник.
    /// </summary>
    public sealed class CombatPacketTests
    {
        private CombatState _state = null!;
        private PacketDispatcher _dispatcher = null!;

        [SetUp]
        public void SetUp()
        {
            _state = new CombatState();
            _dispatcher = new PacketDispatcher(new RecordingLog());
            CombatHandlers.Register(_dispatcher, _state);
        }

        [Test]
        public void WarMode_RecordedFromServer_Peace()
        {
            _state.OnWarMode(true);

            // 0x72 из записанного входа на ModernUO (Bot/Replays/bot-login-modernuo.tsv): стойка мирная.
            Assert.That(_dispatcher.Dispatch(new byte[] { 0x72, 0x00, 0x00, 0x32, 0x00 }), Is.EqualTo(DispatchResult.Handled));

            Assert.That(_state.WarMode, Is.False);
        }

        [Test]
        public void WarMode_On()
        {
            _dispatcher.Dispatch(new byte[] { 0x72, 0x01, 0x00, 0x32, 0x00 });

            Assert.That(_state.WarMode, Is.True);
        }

        [Test]
        public void ChangeCombatant_Serial()
        {
            _dispatcher.Dispatch(new byte[] { 0xAA, 0x00, 0x00, 0x01, 0x00 });

            Assert.That(_state.Combatant, Is.EqualTo(new Serial(0x100)));
        }

        [Test]
        public void Requests_FiveBytesAsServerReads()
        {
            var session = new NetworkSession(new PacketDispatcher(new RecordingLog()), new RecordingLog());
            var sender = new CombatSender(session);

            sender.SendWarMode(true);
            sender.SendAttack(new Serial(0x100));

            Assert.That(session.Outgoing.Pending.ToArray(),
                Is.EqualTo(new byte[] { 0x72, 0x01, 0x00, 0x32, 0x00, 0x05, 0x00, 0x00, 0x01, 0x00 }));
        }
    }
}
