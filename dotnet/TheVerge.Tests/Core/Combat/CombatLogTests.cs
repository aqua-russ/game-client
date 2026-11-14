using System;
using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.Combat;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Tests.Core.Session;

namespace TheVerge.Tests.Core.Combat
{
    /// <summary>
    /// Журнал боя (PLAN Стадия 4, п. 8): события боя как данные — кто, что, по кому, когда. Ограничен по размеру: старое
    /// вытесняется; в установившемся режиме новой памяти не берёт (AGENTS.md §3.1).
    /// </summary>
    public sealed class CombatLogTests
    {
        private static readonly Serial Player = new Serial(0x3);
        private static readonly Serial Rat = new Serial(0x4);

        private FakeClock _clock = null!;
        private CombatLog _log = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock { NowMs = 5000 };
            _log = new CombatLog(_clock, 8);
        }

        [Test]
        public void Swing_WhoWhomWhen()
        {
            _log.OnSwing(Player, Rat);

            var e = _log[0];
            Assert.That(e.Kind, Is.EqualTo(CombatEventKind.Swing));
            Assert.That(e.Source, Is.EqualTo(Player));
            Assert.That(e.Target, Is.EqualTo(Rat));
            Assert.That(e.TimeMs, Is.EqualTo(5000));
        }

        [Test]
        public void Events_InOrderWithFields()
        {
            _log.OnAnimation(Rat, 5);
            _clock.NowMs += 10;
            _log.OnDamage(Player, 3);
            _log.OnEffect(EffectKind.Moving, Player, Rat, 0x36D4, new Point3D(1, 2, 3), 0x21);
            _log.OnScreenEffect(2);
            _log.OnSound(0x1A4, new Point3D(633, 858, 0));

            Assert.That(_log.Count, Is.EqualTo(5));
            Assert.That((_log[0].Kind, _log[0].Source, _log[0].Value), Is.EqualTo((CombatEventKind.Animation, Rat, 5)));
            Assert.That((_log[1].Kind, _log[1].Target, _log[1].Value, _log[1].TimeMs), Is.EqualTo((CombatEventKind.Damage, Player, 3, 5010L)));
            var effect = _log[2];
            Assert.That((effect.Kind, effect.Detail, effect.Source, effect.Target, effect.Value), Is.EqualTo((CombatEventKind.Effect, (int)EffectKind.Moving, Player, Rat, 0x36D4)));
            Assert.That((effect.Position, effect.Hue), Is.EqualTo((new Point3D(1, 2, 3), 0x21)));
            Assert.That((_log[3].Kind, _log[3].Detail, _log[3].Value), Is.EqualTo((CombatEventKind.Effect, (int)EffectKind.Screen, 2)));
            Assert.That((_log[4].Kind, _log[4].Value, _log[4].Position), Is.EqualTo((CombatEventKind.Sound, 0x1A4, new Point3D(633, 858, 0))));
        }

        [Test]
        public void Full_OldestReplaced()
        {
            for (var i = 0; i < 13; i++)
            {
                _log.OnAnimation(Rat, i);
            }

            Assert.That(_log.Count, Is.EqualTo(8));
            Assert.That(_log.Total, Is.EqualTo(13));
            Assert.That(_log[0].Value, Is.EqualTo(5));
            Assert.That(_log[7].Value, Is.EqualTo(12));
        }

        [Test]
        public void Reused_SlotHasNoStaleFields()
        {
            _log = new CombatLog(_clock, 1);
            _log.OnEffect(EffectKind.FixedXYZ, Player, Rat, 0x3709, new Point3D(1, 1, 1), 5);

            _log.OnSwing(Player, Rat);

            Assert.That((_log[0].Value, _log[0].Detail, _log[0].Hue, _log[0].Position), Is.EqualTo((0, 0, 0, Point3D.Zero)));
        }

        [Test]
        public void IndexOutside_Throws()
        {
            _log.OnSwing(Player, Rat);

            Assert.Throws<ArgumentOutOfRangeException>(new Action(() => { _ = _log[1]; }));
        }

        [Test]
        public void Added_RaisedWithEvent()
        {
            var seen = new List<CombatEvent>();
            _log.Added += seen.Add;

            _log.OnSwing(Player, Rat);

            Assert.That(seen.Count, Is.EqualTo(1));
            Assert.That(seen[0].Target, Is.EqualTo(Rat));
        }

        [Test]
        public void Clear_EmptiesButKeepsTotal()
        {
            _log.OnSwing(Player, Rat);

            _log.Clear();

            Assert.That(_log.Count, Is.EqualTo(0));
            Assert.That(_log.Total, Is.EqualTo(1));
        }

        [Test]
        public void DefaultCapacity_FromClientLimits()
        {
            Assert.That(new CombatLog(_clock).Capacity, Is.EqualTo(ClientLimits.CombatLogCapacity));
        }

        [Test]
        public void SteadyState_NoNewMemory()
        {
            var last = 0;
            _log.Added += e => last = e.Value;
            for (var i = 0; i < 100; i++)
            {
                _log.OnAnimation(Rat, i);
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 10000; i++)
            {
                _log.OnSwing(Player, Rat);
                _log.OnAnimation(Rat, i);
                _log.OnSound(0x1A4, Point3D.Zero);
            }

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
            Assert.That(last, Is.EqualTo(0x1A4));
        }
    }
}
