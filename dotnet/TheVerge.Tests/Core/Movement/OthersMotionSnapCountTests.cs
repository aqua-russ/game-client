using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using static TheVerge.Tests.Core.Movement.OthersScene;

namespace TheVerge.Tests.Core.Movement
{
    /// <summary>
    /// Перескоки чужих по причинам для экрана отладки (PLAN Стадия 3, п. 9; ADR 0020 п. 5, уточнение 15 ноября): телепорт
    /// (0x78 с новой клеткой), рывок больше двух клеток одним 0x77, отставание картинки больше ClientLimits.StepBacklogSnapMs.
    /// Появление моба перескоком не считается (как и в общем счёте <see cref="OthersMotion.SnapCount"/>).
    /// </summary>
    public sealed class OthersMotionSnapCountTests
    {
        private OthersScene _s = null!;

        [SetUp]
        public void SetUp() => _s = new OthersScene();

        [TearDown]
        public void TearDown() => _s.Dispose();

        [Test]
        public void EachReason_CountedSeparately_TotalIsSum()
        {
            _s.At(1000);
            _s.Appear(Orc, 200, 200);
            _s.Appear(Rat, 300, 300);

            _s.Incoming(Orc, 210, 200);
            _s.Move(Orc, 214, 200, Direction.East);
            _s.Move(Rat, 301, 300, Direction.East);
            _s.Move(Rat, 302, 300, Direction.East);
            _s.Move(Rat, 303, 300, Direction.East);

            Assert.That(_s.Motion.SnapsOf(SnapReason.Teleported), Is.EqualTo(1));
            Assert.That(_s.Motion.SnapsOf(SnapReason.Jumped), Is.EqualTo(1));
            Assert.That(_s.Motion.SnapsOf(SnapReason.Backlog), Is.EqualTo(1), "три шага шагом по 400 мс разом — отставание 1,2 с, больше 1 с");
            Assert.That(_s.Motion.SnapsOf(SnapReason.Appeared), Is.EqualTo(0));
            Assert.That(_s.Motion.SnapCount, Is.EqualTo(3));
        }
    }
}
