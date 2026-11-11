using NUnit.Framework;
using TheVerge.Core.Primitives;

namespace TheVerge.Tests.Core.Primitives
{
    /// <summary>
    /// Соседняя клетка по направлению — как у сервера: Projects/Server/Mobiles/Movement.cs, Movement.Offset.
    /// Север — y−1, восток — x+1; «Right» — северо-восток, «Down» — юго-восток и т. д.
    /// </summary>
    public sealed class Point3DTests
    {
        private static readonly Point3D Start = new Point3D(1000, 2000, 5);

        [TestCase(Direction.North, 0, -1)]
        [TestCase(Direction.Right, 1, -1)]
        [TestCase(Direction.East, 1, 0)]
        [TestCase(Direction.Down, 1, 1)]
        [TestCase(Direction.South, 0, 1)]
        [TestCase(Direction.Left, -1, 1)]
        [TestCase(Direction.West, -1, 0)]
        [TestCase(Direction.Up, -1, -1)]
        public void GetStepPoint_ShiftsLikeServer(Direction direction, int dx, int dy)
        {
            var next = Start.GetStepPoint(direction);

            Assert.That(next, Is.EqualTo(new Point3D((short)(Start.X + dx), (short)(Start.Y + dy), Start.Z)));
        }

        [Test]
        public void GetStepPoint_RunningFlagGivesSameCell()
        {
            var walk = Start.GetStepPoint(Direction.Down);
            var run = Start.GetStepPoint(Direction.Down | Direction.Running);

            Assert.That(run, Is.EqualTo(walk));
        }

        [Test]
        public void GetStepPoint_KeepsHeight()
        {
            var next = Start.GetStepPoint(Direction.North);

            Assert.That(next.Z, Is.EqualTo(Start.Z), "высоту новой клетки считает проверка шага (Стадия 3), а не сдвиг");
        }
    }
}
