using NUnit.Framework;
using TheVerge.Core.Primitives;

namespace TheVerge.Tests.Core.Primitives
{
    /// <summary>
    /// Номера направлений идут в пакетах как есть (0x02 шаг, 0x21, 0x77, 0x78, 0x20), поэтому обязаны совпадать
    /// с сервером: Projects/Server/Mobiles/Mobile.cs, enum Direction.
    /// </summary>
    public sealed class DirectionTests
    {
        [TestCase(Direction.North, 0x0)]
        [TestCase(Direction.Right, 0x1)]
        [TestCase(Direction.East, 0x2)]
        [TestCase(Direction.Down, 0x3)]
        [TestCase(Direction.South, 0x4)]
        [TestCase(Direction.Left, 0x5)]
        [TestCase(Direction.West, 0x6)]
        [TestCase(Direction.Up, 0x7)]
        public void Direction_MatchesServerNumber(Direction direction, int serverValue)
        {
            Assert.That((int)direction, Is.EqualTo(serverValue));
        }

        [Test]
        public void FacingMaskAndRunningFlag_MatchServer()
        {
            Assert.That((int)Direction.FacingMask, Is.EqualTo(0x7), "Direction.Mask на сервере");
            Assert.That((int)Direction.Running, Is.EqualTo(0x80), "Direction.Running на сервере");
        }

        [Test]
        public void RunningFlag_DoesNotChangeFacing()
        {
            var runningWest = Direction.West | Direction.Running;

            Assert.That(runningWest & Direction.FacingMask, Is.EqualTo(Direction.West));
        }
    }
}
