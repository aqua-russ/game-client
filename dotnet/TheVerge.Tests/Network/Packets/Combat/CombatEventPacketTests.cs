using System;
using NUnit.Framework;
using TheVerge.Core.Combat;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using TheVerge.Network.Packets.Combat;
using TheVerge.Network.Packets.World;
using TheVerge.Network.Session;
using TheVerge.Tests.Core.Session;
using TheVerge.Tests.Network.Session;
using P = TheVerge.Tests.Network.Packets.Combat.ServerCombatEventPackets;

namespace TheVerge.Tests.Network.Packets.Combat
{
    /// <summary>
    /// Боевые события (PLAN Стадия 4, п. 8) разбираются по коду сервера (<see cref="ServerCombatEventPackets"/>) и
    /// попадают в журнал боя: 0x2F замах, 0x6E анимация, 0x0B урон, 0x70 / 0xC0 / 0xC7 эффекты, 0x54 звук. Обрезанный
    /// пакет — ошибка пакета (ReadError), журнал не трогается.
    /// </summary>
    public sealed class CombatEventPacketTests
    {
        private FakeClock _clock = null!;
        private CombatLog _log = null!;
        private WorldEnvironment _environment = null!;
        private PacketDispatcher _dispatcher = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock { NowMs = 100 };
            _log = new CombatLog(_clock);
            _environment = new WorldEnvironment();
            _environment.SoundPlayed += _log.OnSound;
            _dispatcher = new PacketDispatcher(new RecordingLog());
            CombatLogHandlers.Register(_dispatcher, _log);
            WorldHandlers.Register(_dispatcher, new EntityStore(), _environment);
        }

        [Test]
        public void Swing()
        {
            Assert.That(_dispatcher.Dispatch(P.Swing(0x3, 0x4)), Is.EqualTo(DispatchResult.Handled));

            Assert.That((_log[0].Kind, _log[0].Source, _log[0].Target), Is.EqualTo((CombatEventKind.Swing, new Serial(0x3), new Serial(0x4))));
        }

        [Test]
        public void Animation_Action()
        {
            _dispatcher.Dispatch(P.Animation(0x4, 0x0C, (5, 1, 0), true, false));

            Assert.That((_log[0].Kind, _log[0].Source, _log[0].Value), Is.EqualTo((CombatEventKind.Animation, new Serial(0x4), 0x0C)));
        }

        [Test]
        public void Damage_UpTo65535()
        {
            _dispatcher.Dispatch(P.Damage(0x3, 0xFFFF));

            Assert.That((_log[0].Kind, _log[0].Target, _log[0].Value), Is.EqualTo((CombatEventKind.Damage, new Serial(0x3), 0xFFFF)));
        }

        [Test]
        public void ScreenEffect()
        {
            _dispatcher.Dispatch(P.Screen(3));

            Assert.That((_log[0].Kind, _log[0].Detail, _log[0].Value), Is.EqualTo((CombatEventKind.Effect, (int)EffectKind.Screen, 3)));
        }

        [Test]
        public void GraphicalEffect_General()
        {
            _dispatcher.Dispatch(P.Graphical(Sample(0)));

            AssertEffect(EffectKind.Moving, 0);
        }

        [Test]
        public void HuedEffect()
        {
            _dispatcher.Dispatch(P.Hued(Sample(3)));

            AssertEffect(EffectKind.FixedFrom, 0x481);
        }

        [Test]
        public void ParticleEffect()
        {
            Assert.That(_dispatcher.Dispatch(P.Particle(Sample(2), 0x13AE, 0, 0, 0x4, 3)), Is.EqualTo(DispatchResult.Handled));

            AssertEffect(EffectKind.FixedXYZ, 0x481);
        }

        [Test]
        public void Sound_ToLogAndEnvironment()
        {
            _dispatcher.Dispatch(P.Sound(0x1A4, 633, 858, -5));

            Assert.That((_log[0].Kind, _log[0].Value, _log[0].Position), Is.EqualTo((CombatEventKind.Sound, 0x1A4, new Point3D(633, 858, -5))));
            Assert.That((_environment.SoundsPlayed, _environment.LastSound), Is.EqualTo((1L, (ushort)0x1A4)));
        }

        [Test]
        public void Truncated_ReadErrorAndLogUntouched()
        {
            var swing = P.Swing(0x3, 0x4);

            Assert.That(_dispatcher.Dispatch(swing.AsSpan(0, 7)), Is.EqualTo(DispatchResult.ReadError));
            Assert.That(_dispatcher.Dispatch(P.Hued(Sample(3)).AsSpan(0, 20)), Is.EqualTo(DispatchResult.ReadError));

            Assert.That(_log.Count, Is.EqualTo(0));
        }

        private static P.Effect Sample(int type) => new P.Effect
        {
            Type = type, From = 0x3, To = 0x4, ItemId = 0x36D4, FromX = 633, FromY = 858, FromZ = -2, ToX = 634, ToY = 859,
            ToZ = 1, Speed = 7, Duration = 0, FixedDirection = false, Explode = true, Hue = 0x481, RenderMode = 0,
        };

        private void AssertEffect(EffectKind kind, int hue)
        {
            var e = _log[0];
            Assert.That((e.Kind, e.Detail, e.Source, e.Target, e.Value), Is.EqualTo((CombatEventKind.Effect, (int)kind, new Serial(0x3), new Serial(0x4), 0x36D4)));
            Assert.That((e.Position, e.Hue), Is.EqualTo((new Point3D(633, 858, -2), hue)));
        }
    }
}
