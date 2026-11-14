using System;
using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.Combat;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using TheVerge.Network.Packets.Combat;
using TheVerge.Network.Packets.Items;
using TheVerge.Network.Session;
using TheVerge.Tests.Core.Session;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Network.Packets.Combat
{
    /// <summary>
    /// Пакеты смерти так, как их пишет сервер: 0xAF — OutgoingMobilePackets.cs, CreateDeathAnimation (убитый, труп, int 0;
    /// 13 байт); 0x2C — OutgoingPlayerPackets.cs, SendDeathStatus ({ 0x2C, 2 }); 0x89 — UOContent/Items/Misc/Corpses/
    /// CorpsePackets.cs, SendCorpseEquip (длина, труп, пары «слой + 1, предмет», конец — байт 0). Ответ клиента на 0x2C
    /// сервер игнорирует (IncomingPlayerPackets.cs, DeathStatusResponse) — клиент его не шлёт.
    /// </summary>
    public sealed class DeathPacketTests
    {
        private static readonly Serial Corpse = new Serial(0x40000037);

        private EntityStore _store = null!;
        private CombatState _state = null!;
        private CombatLog _log = null!;
        private CorpseEquipment _corpses = null!;
        private PacketDispatcher _dispatcher = null!;

        [SetUp]
        public void SetUp()
        {
            _store = new EntityStore();
            _store.Reset(new PlayerState(new Serial(0x3), 0x190, new Point3D(10, 10, 0), Direction.North, new MapSize(7168, 4096)));
            _store.PlaceInWorld(Corpse, new ItemInfo(0x2006, 0x190, new Point3D(10, 10, 0), 0, 0, 0));
            _state = new CombatState();
            _log = new CombatLog(new FakeClock());
            _corpses = new CorpseEquipment(_store);
            _dispatcher = new PacketDispatcher(new RecordingLog());
            CombatHandlers.Register(_dispatcher, _state);
            CombatLogHandlers.Register(_dispatcher, _log);
            CorpseHandlers.Register(_dispatcher, _corpses);
        }

        [Test]
        public void DeathAnimation_KilledAndCorpse()
        {
            var packet = new List<byte> { 0xAF };
            U32(packet, 0x4);
            U32(packet, Corpse.Value);
            U32(packet, 0);

            Assert.That(_dispatcher.Dispatch(packet.ToArray()), Is.EqualTo(DispatchResult.Handled));

            Assert.That((_log[0].Kind, _log[0].Source, _log[0].Target), Is.EqualTo((CombatEventKind.Death, new Serial(0x4), Corpse)));
        }

        [Test]
        public void DeathStatus_FromServer()
        {
            Assert.That(_dispatcher.Dispatch(new byte[] { 0x2C, 2 }), Is.EqualTo(DispatchResult.Handled));

            Assert.That(_state.Deaths, Is.EqualTo(1));
        }

        [Test]
        public void CorpseEquipment_LayersMinusOne()
        {
            var packet = CorpseEquip((Layer.Hair, 0x7FFFFFFE), (Layer.FacialHair, 0x7FFFFFFD));

            Assert.That(_dispatcher.Dispatch(packet), Is.EqualTo(DispatchResult.Handled));

            Assert.That(_corpses.Get(Corpse), Is.EqualTo(new[]
            {
                new CorpseItem(Layer.Hair, new Serial(0x7FFFFFFE)), new CorpseItem(Layer.FacialHair, new Serial(0x7FFFFFFD)),
            }));
        }

        [Test]
        public void CorpseEquipment_Empty()
        {
            _dispatcher.Dispatch(CorpseEquip());

            Assert.That(_corpses.Get(Corpse), Is.Empty);
            Assert.That(_corpses.Count, Is.EqualTo(1), "пустой список — тоже ответ сервера: вещей на трупе нет");
        }

        [Test]
        public void CorpseEquipment_NoEndMarker_ReadErrorNothingStored()
        {
            var packet = CorpseEquip((Layer.Hair, 0x7FFFFFFE));
            var cut = packet.AsSpan(0, packet.Length - 1).ToArray();
            cut[2] = (byte)cut.Length;

            Assert.That(_dispatcher.Dispatch(cut), Is.EqualTo(DispatchResult.ReadError));

            Assert.That(_corpses.Count, Is.EqualTo(0));
        }

        private static byte[] CorpseEquip(params (Layer Layer, uint Serial)[] items)
        {
            var p = new List<byte> { 0x89, 0, 0 };
            U32(p, Corpse.Value);
            foreach (var (layer, serial) in items)
            {
                p.Add((byte)(layer + 1));
                U32(p, serial);
            }

            p.Add((byte)Layer.Invalid);
            p[1] = (byte)(p.Count >> 8);
            p[2] = (byte)p.Count;
            return p.ToArray();
        }

        private static void U32(List<byte> p, uint v)
        {
            p.Add((byte)(v >> 24));
            p.Add((byte)(v >> 16));
            p.Add((byte)(v >> 8));
            p.Add((byte)v);
        }
    }
}
