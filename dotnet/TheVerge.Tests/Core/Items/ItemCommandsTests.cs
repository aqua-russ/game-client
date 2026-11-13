using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.Items;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Core.World;
using TheVerge.Tests.Core.Session;

namespace TheVerge.Tests.Core.Items
{
    /// <summary>
    /// Двойной клик (PLAN Стадия 2, п. 9а). Сервер выполняет его, только если прошла задержка действий
    /// (IncomingEntityPackets.cs, UseReq: Core.TickCount − NextActionTime ≥ 0; NextActionTime = сейчас + ActionDelay,
    /// 500 мс до AoS — docs/TARGET.md §3), иначе отвечает «подождите». Клиент раньше времени не шлёт.
    /// </summary>
    public sealed class ItemCommandsTests
    {
        private static readonly Serial Player = new Serial(0x3E);
        private static readonly Serial Backpack = new Serial(0x400001E2);

        private EntityStore _store = null!;
        private RecordingItemSender _sender = null!;
        private FakeClock _clock = null!;
        private ItemCommands _commands = null!;

        [SetUp]
        public void SetUp()
        {
            _store = new EntityStore();
            _sender = new RecordingItemSender();
            _clock = new FakeClock { NowMs = 10000 };
            _commands = new ItemCommands(_store, _sender, _clock);
        }

        [Test]
        public void Use_SendsSerial()
        {
            Assert.That(_commands.Use(new Serial(0x40000500)), Is.True);

            Assert.That(_sender.Used, Is.EqualTo(new[] { 0x40000500u }));
        }

        [Test]
        public void Use_SoonerThanActionDelay_NotSent_ThenAllowed()
        {
            _commands.Use(new Serial(0x40000500));

            _clock.NowMs += ServerLimits.ActionDelayMs - 1;
            Assert.That(_commands.Use(new Serial(0x40000501)), Is.False);
            _clock.NowMs += 1;
            Assert.That(_commands.Use(new Serial(0x40000502)), Is.True);

            Assert.That(_sender.Used, Is.EqualTo(new[] { 0x40000500u, 0x40000502u }));
        }

        [TestCase(0x00000000u)]
        [TestCase(0x80000000u)]
        public void Use_InvalidSerial_NotSent(uint value)
        {
            Assert.That(_commands.Use(new Serial(value)), Is.False);
            Assert.That(_sender.Used, Is.Empty);
        }

        [Test]
        public void Use_Mobile_Sent()
        {
            // Двойной клик по мобу сервер тоже принимает (UseReq: s.IsMobile → from.Use(m)).
            Assert.That(_commands.Use(new Serial(0x100)), Is.True);
        }

        [Test]
        public void OpenBackpack_UsesPlayersBackpack()
        {
            _store.Reset(new PlayerState(Player, 0x190, new Point3D(1, 1, 0), Direction.North, new MapSize(7168, 4096)));
            _store.Equip(Backpack, new ItemInfo(0x0E75, 1, Point3D.Zero, 0, 0, 0), Player, Layer.Backpack);

            Assert.That(_commands.OpenBackpack(), Is.True);

            Assert.That(_sender.Used, Is.EqualTo(new[] { Backpack.Value }));
        }

        [Test]
        public void OpenBackpack_NoBackpack_NotSent()
        {
            _store.Reset(new PlayerState(Player, 0x190, new Point3D(1, 1, 0), Direction.North, new MapSize(7168, 4096)));

            Assert.That(_commands.OpenBackpack(), Is.False);
            Assert.That(_sender.Used, Is.Empty);
        }

        private sealed class RecordingItemSender : IItemSender
        {
            public List<uint> Used { get; } = new List<uint>();

            public void SendUse(Serial serial) => Used.Add(serial.Value);
        }
    }
}
