using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;

namespace TheVerge.Tests.Core.Movement
{
    /// <summary>
    /// Таблица «клавиши → направление UO» (PLAN Стадия 3, п. 1; CONTROLS §4): все 16 сочетаний W/A/S/D в обеих раскладках
    /// камеры. Ожидания — из текста PLAN: «север сверху» W = North, D = East, S = South, A = West, W+D = Right, D+S = Down,
    /// S+A = Left, A+W = Up; изометрия: одиночные клавиши — Up, Right, Down, Left, пары — North (W+D), East (D+S),
    /// South (S+A), West (A+W). Противоположные клавиши гасят друг друга.
    /// </summary>
    public sealed class MoveDirectionTableTests
    {
        private const MoveKeys W = MoveKeys.Up;
        private const MoveKeys D = MoveKeys.Right;
        private const MoveKeys S = MoveKeys.Down;
        private const MoveKeys A = MoveKeys.Left;

        [TestCase(W, Direction.North)]
        [TestCase(D, Direction.East)]
        [TestCase(S, Direction.South)]
        [TestCase(A, Direction.West)]
        [TestCase(W | D, Direction.Right)]
        [TestCase(D | S, Direction.Down)]
        [TestCase(S | A, Direction.Left)]
        [TestCase(A | W, Direction.Up)]
        [TestCase(W | D | S, Direction.East)]
        [TestCase(D | S | A, Direction.South)]
        [TestCase(S | A | W, Direction.West)]
        [TestCase(A | W | D, Direction.North)]
        public void NorthUp_KeysGiveDirection(MoveKeys keys, Direction expected)
        {
            bool moves = MoveDirectionTable.TryGetDirection(keys, CameraLayout.NorthUp, out var direction);

            Assert.That(moves, Is.True);
            Assert.That(direction, Is.EqualTo(expected));
        }

        [TestCase(W, Direction.Up)]
        [TestCase(D, Direction.Right)]
        [TestCase(S, Direction.Down)]
        [TestCase(A, Direction.Left)]
        [TestCase(W | D, Direction.North)]
        [TestCase(D | S, Direction.East)]
        [TestCase(S | A, Direction.South)]
        [TestCase(A | W, Direction.West)]
        [TestCase(W | D | S, Direction.Right)]
        [TestCase(D | S | A, Direction.Down)]
        [TestCase(S | A | W, Direction.Left)]
        [TestCase(A | W | D, Direction.Up)]
        public void Isometric_KeysGiveDirection(MoveKeys keys, Direction expected)
        {
            bool moves = MoveDirectionTable.TryGetDirection(keys, CameraLayout.Isometric, out var direction);

            Assert.That(moves, Is.True);
            Assert.That(direction, Is.EqualTo(expected));
        }

        [TestCase(MoveKeys.None, CameraLayout.NorthUp)]
        [TestCase(W | S, CameraLayout.NorthUp)]
        [TestCase(D | A, CameraLayout.NorthUp)]
        [TestCase(W | D | S | A, CameraLayout.NorthUp)]
        [TestCase(MoveKeys.None, CameraLayout.Isometric)]
        [TestCase(W | S, CameraLayout.Isometric)]
        [TestCase(D | A, CameraLayout.Isometric)]
        [TestCase(W | D | S | A, CameraLayout.Isometric)]
        public void NoKeysOrOppositeKeys_NoStep(MoveKeys keys, CameraLayout layout)
        {
            bool moves = MoveDirectionTable.TryGetDirection(keys, layout, out _);

            Assert.That(moves, Is.False);
        }

        [TestCase(W | D, true)]
        [TestCase(D | S, true)]
        [TestCase(S | A, true)]
        [TestCase(A | W, true)]
        [TestCase(W, false)]
        [TestCase(D, false)]
        [TestCase(W | D | S, false)]
        [TestCase(W | S, false)]
        [TestCase(MoveKeys.None, false)]
        public void IsScreenDiagonal_OnlyForAdjacentPair(MoveKeys keys, bool expected)
        {
            Assert.That(MoveDirectionTable.IsScreenDiagonal(keys), Is.EqualTo(expected));
        }
    }
}
