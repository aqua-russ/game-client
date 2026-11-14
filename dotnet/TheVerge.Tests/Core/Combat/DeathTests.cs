using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.Combat;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using TheVerge.Tests.Core.Session;

namespace TheVerge.Tests.Core.Combat
{
    /// <summary>
    /// Смерть (PLAN Стадия 4, п. 9) — только по пакетам сервера: своя — статус 0x2C (Mobile.OnDeath: SendDeathStatus) и
    /// тело призрака (Body.IsGhost сервера: 402, 403, 607, 608, 694, 695, 970); чужая — анимация смерти 0xAF (кто, труп)
    /// в журнале боя; вещи трупа по слоям — 0x89 (<see cref="CorpseEquipment"/>), пока труп есть в мире.
    /// </summary>
    public sealed class DeathTests
    {
        private static readonly Serial Player = new Serial(0x3);
        private static readonly Serial Corpse = new Serial(0x40000037);
        private static readonly Serial Hair = new Serial(0x7FFFFFFE);

        [TestCase((ushort)402, true)]
        [TestCase((ushort)403, true)]
        [TestCase((ushort)607, true)]
        [TestCase((ushort)608, true)]
        [TestCase((ushort)694, true)]
        [TestCase((ushort)695, true)]
        [TestCase((ushort)970, true)]
        [TestCase((ushort)400, false)]
        [TestCase((ushort)401, false)]
        [TestCase((ushort)0xEE, false)]
        public void Ghost_AsServer(ushort body, bool ghost)
        {
            Assert.That(Bodies.IsGhost(body), Is.EqualTo(ghost));
        }

        [Test]
        public void Mobile_IsGhost_ByBodyFromServer()
        {
            var store = NewStore();

            store.UpdateMobile(Player, new MobileInfo(0x192, new Point3D(10, 10, 0), Direction.North, 0, 0, 1));

            Assert.That(store.Player!.IsGhost, Is.True);
        }

        [Test]
        public void DeathStatus_CountsAndRaises()
        {
            var state = new CombatState();
            var died = 0;
            state.Died += () => died++;

            state.OnDeathStatus();

            Assert.That((state.Deaths, died), Is.EqualTo((1, 1)));
            state.Reset();
            Assert.That(state.Deaths, Is.EqualTo(0));
        }

        [Test]
        public void DeathAnimation_InJournal()
        {
            var log = new CombatLog(new FakeClock());

            log.OnDeath(Player, Corpse);

            Assert.That((log[0].Kind, log[0].Source, log[0].Target), Is.EqualTo((CombatEventKind.Death, Player, Corpse)));
        }

        [Test]
        public void CorpseEquipment_SetGetReplace()
        {
            var store = NewStore();
            PlaceCorpse(store);
            var corpses = new CorpseEquipment(store);
            var changed = new List<Serial>();
            corpses.Changed += changed.Add;

            corpses.Set(Corpse, new[] { new CorpseItem(Layer.Hair, Hair), new CorpseItem(Layer.FacialHair, new Serial(0x7FFFFFFD)) });
            corpses.Set(Corpse, new[] { new CorpseItem(Layer.Hair, Hair) });

            Assert.That(corpses.Get(Corpse), Is.EqualTo(new[] { new CorpseItem(Layer.Hair, Hair) }));
            Assert.That(changed, Is.EqualTo(new[] { Corpse, Corpse }));
        }

        [Test]
        public void CorpseEquipment_ForgottenWithCorpse()
        {
            var store = NewStore();
            PlaceCorpse(store);
            var corpses = new CorpseEquipment(store);
            corpses.Set(Corpse, new[] { new CorpseItem(Layer.Hair, Hair) });

            store.Remove(Corpse);

            Assert.That(corpses.Get(Corpse), Is.Empty);
            Assert.That(corpses.Count, Is.EqualTo(0));
        }

        [Test]
        public void CorpseEquipment_ForgottenOnNewLogin()
        {
            var store = NewStore();
            PlaceCorpse(store);
            var corpses = new CorpseEquipment(store);
            corpses.Set(Corpse, new[] { new CorpseItem(Layer.Hair, Hair) });

            store.Reset(new PlayerState(Player, 0x190, new Point3D(10, 10, 0), Direction.North, new MapSize(7168, 4096)));

            Assert.That(corpses.Count, Is.EqualTo(0));
        }

        [Test]
        public void CorpseEquipment_UnknownCorpse_Ignored()
        {
            var corpses = new CorpseEquipment(NewStore());

            corpses.Set(Corpse, new[] { new CorpseItem(Layer.Hair, Hair) });

            Assert.That((corpses.Count, corpses.UnknownCorpse), Is.EqualTo((0, 1L)));
        }

        private static EntityStore NewStore()
        {
            var store = new EntityStore();
            store.Reset(new PlayerState(Player, 0x190, new Point3D(10, 10, 0), Direction.North, new MapSize(7168, 4096)));
            return store;
        }

        private static void PlaceCorpse(EntityStore store) =>
            store.PlaceInWorld(Corpse, new ItemInfo(0x2006, 0x190, new Point3D(10, 10, 0), 0, 0, 0));
    }
}
