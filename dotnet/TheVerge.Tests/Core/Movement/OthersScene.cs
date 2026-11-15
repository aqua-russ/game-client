using System;
using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using TheVerge.Tests.Core.Session;
using TheVerge.Tests.Core.World;

namespace TheVerge.Tests.Core.Movement
{
    /// <summary>
    /// Мир с чужими мобами для тестов <see cref="OthersMotion"/>: пакеты 0x78 (<see cref="Appear"/>, <see cref="Incoming"/>)
    /// и 0x77 (<see cref="Move"/>) идут так же, как из обработчиков: сначала мир, потом учётчик; тик — <see cref="Tick"/>.
    /// </summary>
    internal sealed class OthersScene : IDisposable
    {
        public static readonly Serial Orc = WorldSamples.Orc;
        public static readonly Serial Rat = new Serial(0x00000200);

        public OthersScene()
        {
            Store.Reset(WorldSamples.PlayerAt(100, 100));
            Motion = new OthersMotion(Store, Clock);
            Motion.StepStarted += OnStarted;
            Motion.Snapped += OnSnapped;
        }

        public EntityStore Store { get; } = new EntityStore();

        public FakeClock Clock { get; } = new FakeClock();

        public OthersMotion Motion { get; }

        public List<ShownStep> Started { get; } = new List<ShownStep>();

        public List<MotionSnap> Snaps { get; } = new List<MotionSnap>();

        public static Direction Dir(Direction facing, bool running) => running ? facing | Direction.Running : facing;

        /// <summary>0x78: моб с этой клеткой (появился, телепорт или то же место).</summary>
        public void Incoming(Serial serial, short x, short y, sbyte z = 0)
        {
            Store.UpdateMobile(serial, new MobileInfo(0x0190, new Point3D(x, y, z), Direction.South, 0, 0, 1));
            Motion.OnIncoming(serial);
        }

        public void Appear(Serial serial, short x, short y, sbyte z = 0) => Incoming(serial, x, y, z);

        /// <summary>0x77: моб на этой клетке с этим направлением (шаг, поворот или смена флагов).</summary>
        public void Move(Serial serial, short x, short y, Direction direction, sbyte z = 0)
        {
            Store.MoveMobile(serial, new MobileInfo(0x0190, new Point3D(x, y, z), direction, 0, 0, 1));
            Motion.OnMoving(serial);
        }

        /// <summary>Сесть на коня: предмет на слое Layer.Mount (как приходит в 0x78 и 0x2E).</summary>
        public void Mount(Serial serial) =>
            Store.Equip(new Serial(0x40001000 + serial.Value), new ItemInfo(0x3EA2, 1, Point3D.Zero, 0, 0, 0), serial, Layer.Mount);

        public void At(long ms) => Clock.NowMs = ms;

        public void Tick(long ms)
        {
            Clock.NowMs = ms;
            Motion.Update(ms);
        }

        public ShownStep Step(Serial serial)
        {
            Assert.That(Motion.TryGetStep(serial, out var step), Is.True, "моб " + serial + " не учтён");
            return step;
        }

        /// <summary>Перестать записывать события (списки растут — для проверки памяти не годятся).</summary>
        public void StopRecording()
        {
            Motion.StepStarted -= OnStarted;
            Motion.Snapped -= OnSnapped;
        }

        public void Dispose()
        {
            StopRecording();
            Motion.Dispose();
        }

        private void OnStarted(Serial serial, ShownStep step) => Started.Add(step);

        private void OnSnapped(Serial serial, MotionSnap snap) => Snaps.Add(snap);
    }
}
