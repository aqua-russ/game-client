using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TheVerge.Core.Items;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using TheVerge.Network.Framing;
using TheVerge.Network.IO;
using TheVerge.Network.Packets.Items;
using TheVerge.Network.Session;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Network.Packets.Items
{
    /// <summary>
    /// Контейнеры (PLAN Стадия 2, п. 9а), байты — Projects/Server/Network/Packets/OutgoingContainerPackets.cs для клиента
    /// 7.0.95.0 (HighSeas, ContainerGridLines): 0x24 SendDisplayContainer (Serial, ushort окно, short 0x7D), 0x3C
    /// SendContainerContent (ushort длина, ushort число, по 20 байт: Serial, ushort графика, byte 0, ushort количество,
    /// short X, Y, byte клетка сетки, Serial контейнера, ushort цвет), 0x25 SendContainerContentUpdate (те же 20 байт).
    /// Двойной клик 0x06 — IncomingEntityPackets.cs, UseReq (ReadUInt32, длина 5). Сервер шлёт 0x24 и 0x3C парой
    /// (Container.DisplayTo), 0x3C — всегда, даже пустой.
    /// </summary>
    public sealed class ContainerPacketTests
    {
        private const uint Backpack = 0x400001E2;
        private const uint Apple = 0x40000300;
        private const uint Gold = 0x40000301;

        private EntityStore _store = null!;
        private ContainerContents _contents = null!;
        private PacketDispatcher _dispatcher = null!;

        [SetUp]
        public void SetUp()
        {
            _store = new EntityStore();
            _contents = new ContainerContents(_store);
            _dispatcher = new PacketDispatcher(new RecordingLog());
            ItemHandlers.Register(_dispatcher, _contents);
            _store.PlaceInWorld(new Serial(Backpack), new ItemInfo(0x0E75, 1, new Point3D(10, 10, 0), 0, 0, 0));
        }

        [Test]
        public void DoubleClick_5Bytes_Serial()
        {
            var bytes = PacketBytes.Write((ref PacketWriter w) => DoubleClickPacket.Write(ref w, new Serial(Backpack)));

            Assert.That(bytes, Is.EqualTo(new byte[] { 0x06, 0x40, 0x00, 0x01, 0xE2 }));
            Assert.That(bytes.Length, Is.EqualTo(PacketTable.Get(PacketDirection.ClientToServer, 0x06).FixedLength));
        }

        [Test]
        public void ItemSender_WritesDoubleClickToOutgoing()
        {
            var session = new NetworkSession(new PacketDispatcher(new RecordingLog()), new RecordingLog());

            new ItemSender(session).SendUse(new Serial(Backpack));

            Assert.That(session.Outgoing.Pending.ToArray(), Is.EqualTo(new byte[] { 0x06, 0x40, 0x00, 0x01, 0xE2 }));
        }

        [Test]
        public void DisplayContainer_OpenedEventWithGump()
        {
            var opened = new List<(uint, ushort)>();
            _contents.ContainerOpened += (serial, gump) => opened.Add((serial.Value, gump));

            _dispatcher.Dispatch(new byte[] { 0x24, 0x40, 0x00, 0x01, 0xE2, 0x00, 0x3C, 0x00, 0x7D });

            Assert.That(opened, Is.EqualTo(new[] { (Backpack, (ushort)0x003C) }));
            Assert.That(_contents.OpenedContainer.Value, Is.EqualTo(Backpack));
            Assert.That(_contents.OpenedGump, Is.EqualTo((ushort)0x003C));
        }

        [Test]
        public void DisplayContainer_Spellbook()
        {
            _dispatcher.Dispatch(new byte[] { 0x24, 0x40, 0x00, 0x01, 0xE2, 0xFF, 0xFF, 0x00, 0x7D });

            Assert.That(_contents.OpenedGump, Is.EqualTo(ContainerContents.SpellbookGump));
        }

        [Test]
        public void ContainerContent_ItemsWithPlaceAndHue()
        {
            _dispatcher.Dispatch(Content(new Slot { Serial = Apple, Amount = 5, X = 44, Y = 65, Grid = 2, Hue = 0x0021 }.Bytes(), new Slot { Serial = Gold, Graphic = 0x0EED, Amount = 1000, X = 90, Y = 70 }.Bytes()));

            var apple = _store.FindItem(new Serial(Apple))!;
            Assert.That(apple.Container!.Serial.Value, Is.EqualTo(Backpack));
            Assert.That(apple.Graphic, Is.EqualTo((ushort)0x09D0));
            Assert.That(apple.Amount, Is.EqualTo((ushort)5));
            Assert.That(apple.Position, Is.EqualTo(new Point3D(44, 65, 0)));
            Assert.That(apple.GridIndex, Is.EqualTo((byte)2));
            Assert.That(apple.Hue, Is.EqualTo((ushort)0x0021));
            Assert.That(_store.FindItem(new Serial(Backpack))!.Contents.Count, Is.EqualTo(2));
        }

        [Test]
        public void ContainerContent_ContentReceivedAfterWholeList()
        {
            var received = new List<(uint, int)>();
            _contents.ContentReceived += serial => received.Add((serial.Value, _store.FindItem(serial)!.Contents.Count));
            _dispatcher.Dispatch(new byte[] { 0x24, 0x40, 0x00, 0x01, 0xE2, 0x00, 0x3C, 0x00, 0x7D });

            _dispatcher.Dispatch(Content(new Slot { Serial = Apple }.Bytes(), new Slot { Serial = Gold }.Bytes()));
            _dispatcher.Dispatch(Content());

            Assert.That(received, Is.EqualTo(new[] { (Backpack, 2) }), "пустой 0x3C без своего 0x24 ни к чему не относится");
        }

        [Test]
        public void ContainerContent_ReplacesContents()
        {
            _dispatcher.Dispatch(Content(new Slot { Serial = Apple }.Bytes(), new Slot { Serial = Gold, Graphic = 0x0EED }.Bytes()));

            _dispatcher.Dispatch(Content(new Slot { Serial = Gold, Graphic = 0x0EED, Amount = 7 }.Bytes()));

            Assert.That(_store.FindItem(new Serial(Apple)), Is.Null);
            Assert.That(_store.FindItem(new Serial(Gold))!.Amount, Is.EqualTo((ushort)7));
            Assert.That(_store.FindItem(new Serial(Backpack))!.Contents.Count, Is.EqualTo(1));
        }

        [Test]
        public void ContainerContent_EmptyAfterOpen_ClearsOpenedContainer()
        {
            _dispatcher.Dispatch(Content(new Slot { Serial = Apple }.Bytes()));

            _dispatcher.Dispatch(new byte[] { 0x24, 0x40, 0x00, 0x01, 0xE2, 0x00, 0x3C, 0x00, 0x7D });
            _dispatcher.Dispatch(Content());

            Assert.That(_store.FindItem(new Serial(Backpack))!.Contents, Is.Empty);
            Assert.That(_store.FindItem(new Serial(Apple)), Is.Null);
        }

        [Test]
        public void ContainerContent_EmptyWithoutOpen_NothingCleared()
        {
            _dispatcher.Dispatch(Content(new Slot { Serial = Apple }.Bytes()));
            _dispatcher.Dispatch(new byte[] { 0x24, 0x40, 0x00, 0x01, 0xE2, 0x00, 0x3C, 0x00, 0x7D });
            _dispatcher.Dispatch(Content(new Slot { Serial = Apple }.Bytes()));

            _dispatcher.Dispatch(Content());

            Assert.That(_store.FindItem(new Serial(Apple)), Is.Not.Null, "пустой 0x3C без своего 0x24 не к чему отнести");
        }

        [Test]
        public void ContainerContent_UnknownContainer_StubCounted()
        {
            _dispatcher.Dispatch(Content(new Slot { Serial = Apple, Container = 0x40000999 }.Bytes()));

            Assert.That(_store.FindItem(new Serial(0x40000999))!.Place, Is.EqualTo(ItemPlace.Unplaced));
            Assert.That(_store.Metrics.StubContainers, Is.EqualTo(1));
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void ContainerContent_LengthNotMatchingCount_SkippedWorldUnchanged(int delta)
        {
            var good = Content(new Slot { Serial = Apple }.Bytes());
            var bad = new byte[good.Length + delta];
            Array.Copy(good, bad, Math.Min(good.Length, bad.Length));
            bad[1] = (byte)(bad.Length >> 8);
            bad[2] = (byte)bad.Length;

            Assert.That(_dispatcher.Dispatch(bad), Is.EqualTo(DispatchResult.ReadError));
            Assert.That(_store.FindItem(new Serial(Apple)), Is.Null);
        }

        [Test]
        public void ContainerContent_Spellbook_VirtualSerials()
        {
            // SendOldSpellbookContent: Serial 0x7FFFFFFF − i, графика 0, количество — номер заклинания.
            _dispatcher.Dispatch(Content(new Slot { Serial = 0x7FFFFFFF, Graphic = 0, X = 0, Y = 0 }.Bytes(), new Slot { Serial = 0x7FFFFFFE, Graphic = 0, Amount = 2, X = 0, Y = 0 }.Bytes()));

            Assert.That(_store.FindItem(new Serial(0x7FFFFFFE))!.Amount, Is.EqualTo((ushort)2));
        }

        [Test]
        public void ContainerItem_AddsOrUpdatesOne()
        {
            _dispatcher.Dispatch(ContainerItem(new Slot { Serial = Apple, Amount = 3, X = 50, Y = 60, Grid = 1, Hue = 0x0021 }.Bytes()));
            _dispatcher.Dispatch(ContainerItem(new Slot { Serial = Apple, Amount = 4, X = 51, Y = 61, Grid = 1, Hue = 0x0021 }.Bytes()));

            var apple = _store.FindItem(new Serial(Apple))!;
            Assert.That(apple.Amount, Is.EqualTo((ushort)4));
            Assert.That(apple.Position, Is.EqualTo(new Point3D(51, 61, 0)));
            Assert.That(_store.FindItem(new Serial(Backpack))!.Contents.Count, Is.EqualTo(1));
        }

        [TestCase(0x00000000u)]
        [TestCase(0x00000100u)]
        public void ContainerItem_ParentNotItem_Rejected(uint parent)
        {
            _dispatcher.Dispatch(ContainerItem(new Slot { Serial = Apple, Amount = 3, X = 50, Y = 60, Grid = 1, Container = parent }.Bytes()));

            Assert.That(_store.FindItem(new Serial(Apple)), Is.Null);
            Assert.That(_store.Metrics.Rejected, Is.EqualTo(1));
        }

        [Test]
        public void ContainerItem_Cycle_RejectedWorldUnchanged()
        {
            _dispatcher.Dispatch(ContainerItem(new Slot { Serial = Apple, Graphic = 0x0E75 }.Bytes()));

            _dispatcher.Dispatch(ContainerItem(new Slot { Serial = Backpack, Graphic = 0x0E75, Container = Apple }.Bytes()));

            Assert.That(_store.FindItem(new Serial(Backpack))!.Place, Is.EqualTo(ItemPlace.World));
            Assert.That(_store.Metrics.Rejected, Is.EqualTo(1));
        }

        /// <summary>Предмет в контейнере — 20 байт, как пишет сервер (поля — в описании класса).</summary>
        private sealed class Slot
        {
            public uint Serial { get; set; }
            public ushort Graphic { get; set; } = 0x09D0;
            public ushort Amount { get; set; } = 1;
            public short X { get; set; } = 1;
            public short Y { get; set; } = 1;
            public byte Grid { get; set; }
            public uint Container { get; set; } = Backpack;
            public ushort Hue { get; set; }

            public byte[] Bytes()
            {
                var e = new List<byte>();
                U32(e, Serial);
                U16(e, Graphic);
                e.Add(0);
                U16(e, Amount);
                U16(e, (ushort)X);
                U16(e, (ushort)Y);
                e.Add(Grid);
                U32(e, Container);
                U16(e, Hue);
                return e.ToArray();
            }
        }

        private static byte[] Content(params byte[][] entries)
        {
            var p = new List<byte> { 0x3C, 0, 0 };
            U16(p, (ushort)entries.Length);
            foreach (var entry in entries)
            {
                p.AddRange(entry);
            }

            var bytes = p.ToArray();
            bytes[1] = (byte)(bytes.Length >> 8);
            bytes[2] = (byte)bytes.Length;
            return bytes;
        }

        private static byte[] ContainerItem(byte[] entry) => new byte[] { 0x25 }.Concat(entry).ToArray();

        private static void U16(List<byte> p, ushort value)
        {
            p.Add((byte)(value >> 8));
            p.Add((byte)value);
        }

        private static void U32(List<byte> p, uint value)
        {
            U16(p, (ushort)(value >> 16));
            U16(p, (ushort)value);
        }
    }
}
