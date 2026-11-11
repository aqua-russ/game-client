using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TheVerge.Network.Framing;

namespace TheVerge.Tests.Network.Framing
{
    /// <summary>
    /// Сгенерированная таблица пакетов (PLAN 0.5) против её источника — docs/protocol/packets.csv (ADR 0010 п. 7):
    /// каждая строка матрицы даёт ту же длину и имя, а всё, чего в матрице нет, — «неизвестен».
    /// Ожидания берутся из CSV, а не из кода генератора.
    /// </summary>
    public sealed class PacketTableTests
    {
        private static readonly PacketDirection[] Directions =
        {
            PacketDirection.ClientToServer,
            PacketDirection.ServerToClient,
        };

        private static IEnumerable<TestCaseData> MatrixRows() =>
            PacketMatrix.Rows.Select(row => new TestCaseData(row).SetName("Get_MatrixRow_" + row));

        [Test]
        public void Matrix_HasRowsInBothDirections()
        {
            foreach (var direction in Directions)
            {
                Assert.That(PacketMatrix.Rows.Any(row => row.Direction == direction), Is.True, direction.ToString());
            }
        }

        [TestCaseSource(nameof(MatrixRows))]
        public void Get_MatrixRow_ReturnsSameLengthAndName(PacketMatrixRow row)
        {
            var info = row.SubCommand.HasValue
                ? PacketTable.Get(row.Direction, row.Id, row.SubCommand.Value)
                : PacketTable.Get(row.Direction, row.Id);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(info.IsKnown, Is.True, "IsKnown");
                Assert.That(info.IsVariable, Is.EqualTo(!row.FixedLength.HasValue), "IsVariable");
                Assert.That(info.FixedLength, Is.EqualTo(row.FixedLength ?? 0), "FixedLength");
                Assert.That(info.Name, Is.EqualTo(row.Name), "Name");
            }
        }

        [TestCase(PacketDirection.ClientToServer)]
        [TestCase(PacketDirection.ServerToClient)]
        public void Get_IdAbsentFromMatrix_IsUnknown(PacketDirection direction)
        {
            var absent = AbsentIds(direction);

            Assert.That(absent, Is.Not.Empty);
            using (Assert.EnterMultipleScope())
            {
                foreach (var id in absent)
                {
                    Assert.That(PacketTable.Get(direction, id).IsKnown, Is.False, "0x" + id.ToString("X2"));
                }
            }
        }

        [TestCase(PacketDirection.ClientToServer)]
        [TestCase(PacketDirection.ServerToClient)]
        public void Get_SubCommandAbsentFromMatrix_IsUnknown(PacketDirection direction)
        {
            var known = new HashSet<(byte, ushort)>(PacketMatrix.Rows
                .Where(row => row.Direction == direction && row.SubCommand.HasValue)
                .Select(row => (row.Id, row.SubCommand!.Value)));
            var subCommands = Enumerable.Range(0, 0x100).Select(sub => (ushort)sub).Concat(new ushort[] { 0x100, 0xFFFF });

            using (Assert.EnterMultipleScope())
            {
                for (var id = 0; id <= 0xFF; id++)
                {
                    foreach (var sub in subCommands.Where(sub => !known.Contains(((byte)id, sub))))
                    {
                        Assert.That(PacketTable.Get(direction, (byte)id, sub).IsKnown, Is.False,
                            "0x" + id.ToString("X2") + " 0x" + sub.ToString("X2"));
                    }
                }
            }
        }

        [Test]
        public void Get_UnknownId_HasNoLengthAndNoName()
        {
            var info = PacketTable.Get(PacketDirection.ServerToClient, AbsentIds(PacketDirection.ServerToClient)[0]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(info.IsKnown, Is.False);
                Assert.That(info.IsVariable, Is.False);
                Assert.That(info.FixedLength, Is.EqualTo(0));
                Assert.That(info.Name, Is.Empty);
            }
        }

        private static List<byte> AbsentIds(PacketDirection direction)
        {
            var known = new HashSet<byte>(PacketMatrix.Rows.Where(row => row.Direction == direction).Select(row => row.Id));
            return Enumerable.Range(0, 0x100).Select(id => (byte)id).Where(id => !known.Contains(id)).ToList();
        }
    }
}
