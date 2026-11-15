using System;
using NUnit.Framework;
using TheVerge.Core.Multis;
using TheVerge.Core.Ports;
using TheVerge.Core.Tiles;
using TheVerge.Data.Files;
using TheVerge.Data.Multis;
using TheVerge.Tests.Data.Files;
using Part = TheVerge.Tests.Data.Multis.MultiFileBuilder.Part;

namespace TheVerge.Tests.Data.Multis
{
    /// <summary>
    /// Дома и корабли (PLAN Стадия 2, п. 6): номер → части (номер, сдвиг x/y/z, флаги) и границы — из multi.mul (запись
    /// 16 или 12 байт) и MultiCollection.uop (zlib), как у сервера (MultiData.cs: LoadMul, LoadUOP, MultiComponentList).
    /// Неизвестный номер — пусто; сломанный файл — ошибка словами.
    /// </summary>
    public sealed class MultiTableTests
    {
        private TempDataFolder _folder = null!;

        [SetUp]
        public void SetUp() => _folder = new TempDataFolder();

        [TearDown]
        public void TearDown() => _folder.Dispose();

        [TestCase(true)]
        [TestCase(false)]
        public void Mul_PartsAndBounds_AsServer(bool postHs)
        {
            var table = LoadMul(Example(), postHs, 10);

            AssertExampleHouse(table.Get(1));
            AssertPositiveHouse(table.Get(2));
            Assert.That(table.Count, Is.EqualTo(2));
        }

        [Test]
        public void Mul16_KeepsHigh64BitFlags_Mul12_Only32()
        {
            var file = new MultiFileBuilder();
            file.Add(3, new Part(5, 0, 0, 0, 0x0000000100000200UL));

            Assert.That((ulong)LoadMul(file, true, 4).Get(3).Components[0].Flags, Is.EqualTo(0x0000000100000200UL));
            Assert.That((ulong)LoadMul(file, false, 4).Get(3).Components[0].Flags, Is.EqualTo(0x200UL));
        }

        [Test]
        public void Uop_PartsBoundsAndFlagCodes_AsServer()
        {
            var file = Example();
            file.Add(7,
                new Part(0x20, 1, 1, 0, 0, uopFlagCode: 0, clilocs: 2),
                new Part(0x21, 2, 2, 0, 0, uopFlagCode: 1),
                new Part(0x22, 3, 3, 0, 0, uopFlagCode: 257, clilocs: 1),
                new Part(0x23, 4, 4, 0, 0, uopFlagCode: 7));
            var table = LoadUop(file.BuildUop());

            AssertExampleHouse(table.Get(1));
            AssertPositiveHouse(table.Get(2));
            var codes = table.Get(7).Components;
            Assert.That(codes[0].Flags, Is.EqualTo(TileFlags.Background));
            Assert.That(codes[1].Flags, Is.EqualTo(TileFlags.None));
            Assert.That(codes[2].Flags, Is.EqualTo(TileFlags.Generic));
            Assert.That(codes[3].Flags, Is.EqualTo(TileFlags.Background));
            Assert.That(table.Get(7).MaxX, Is.EqualTo(4), "часть с кодом 1 (None) в границы не входит, с 7 — входит");
            Assert.That(table.Count, Is.EqualTo(3));
        }

        [Test]
        public void UnknownId_Empty_IdMasked()
        {
            var table = LoadMul(Example(), true, 10);

            Assert.That(table.Get(5), Is.SameAs(MultiComponents.Empty));
            Assert.That(table.Get(5).Width, Is.Zero);
            Assert.That(table.Get(0x4000 + 1).Components.Count, Is.EqualTo(4), "номер по маске 0x3FFF");
        }

        [Test]
        public void MulRecordShorterThanPart_NoPartsSize1x1()
        {
            // Сервер заводит такой номер (длина больше 0), но частей в нём 0 — границы остаются (0, 0), размер 1×1.
            var file = new MultiFileBuilder();
            file.BuildMul(true, 3, out var index, out var data);
            BitConverter.GetBytes(0).CopyTo(index, 12);
            BitConverter.GetBytes(10).CopyTo(index, 16);
            var table = Load(index, data, true);

            Assert.That(table.Get(1).Components, Is.Empty);
            Assert.That(table.Get(1).Width, Is.EqualTo(1));
            Assert.That(table.Get(1).Height, Is.EqualTo(1));
            Assert.That(table.Count, Is.EqualTo(1));
        }

        [Test]
        public void Uop_NotCompressed_ReadAsStored()
        {
            // Несжатый кусок сервер не читает из файла (MultiData.LoadUOP берёт старое содержимое буфера); в настоящих
            // файлах все куски сжаты. Клиент читает такой кусок как есть.
            var table = LoadUop(Example().BuildUop(compress: false));

            AssertExampleHouse(table.Get(1));
        }

        [Test]
        public void Uop_BadZlib_Error()
        {
            var file = Example();
            var bytes = file.BuildUop();
            var raw = MultiFileBuilder.Zlib(MultiFileBuilder.UopEntry(file.Multis[1]));
            var at = IndexOf(bytes, raw);
            bytes[at + raw.Length - 1] ^= 0xFF; // контрольная сумма Adler-32 в конце

            Assert.That(LoadUopError(bytes), Does.Contain("MultiCollection.uop"));
        }

        [Test]
        public void Uop_TooManyClilocs_Error()
        {
            var file = new MultiFileBuilder();
            file.Add(1, new Part(1, 0, 0, 0, 0, uopFlagCode: 1));
            var raw = MultiFileBuilder.UopEntry(file.Multis[1]);
            BitConverter.GetBytes(1000u).CopyTo(raw, 18);
            var builder = new UopFileBuilder(4, 10);
            builder.AddCompressed("build/multicollection/000001.bin", MultiFileBuilder.Zlib(raw), raw.Length);

            Assert.That(LoadUopError(builder.Build()), Does.Contain("MultiCollection.uop"));
        }

        [Test]
        public void Uop_ClilocsEatNextPart_ErrorNotCrash()
        {
            // Число частей проверяется по размеру частей без clilocs, а clilocs сдвигают следующую часть: испорченный список
            // clilocs первой части не оставляет места второй — ошибка словами, а не падение на чтении за концом куска.
            var file = new MultiFileBuilder();
            file.Add(1, new Part(1, 0, 0, 0, 0, uopFlagCode: 1), new Part(2, 1, 0, 0, 0, uopFlagCode: 1));
            var raw = MultiFileBuilder.UopEntry(file.Multis[1]);
            BitConverter.GetBytes(3u).CopyTo(raw, 18);
            var builder = new UopFileBuilder(4, 10);
            builder.AddCompressed("build/multicollection/000001.bin", MultiFileBuilder.Zlib(raw), raw.Length);

            Assert.That(LoadUopError(builder.Build()), Does.Contain("MultiCollection.uop"));
        }

        [Test]
        public void Uop_LengthMismatch_Error()
        {
            var file = new MultiFileBuilder();
            file.Add(1, new Part(1, 0, 0, 0, 0, uopFlagCode: 1));
            var raw = MultiFileBuilder.UopEntry(file.Multis[1]);
            var builder = new UopFileBuilder(4, 10);
            builder.AddCompressed("build/multicollection/000001.bin", MultiFileBuilder.Zlib(raw), raw.Length + 5);

            Assert.That(LoadUopError(builder.Build()), Does.Contain("MultiCollection.uop"));
        }

        [Test]
        public void MulBeyondFile_Error()
        {
            var file = Example();
            file.BuildMul(true, 10, out var index, out var data);
            Array.Resize(ref data, data.Length - 4);
            var path = _folder.Bytes("multi.idx", index);

            var table = MultiTable.Load(MultiFiles.Mul(path, _folder.Bytes("multi.mul", data), true), out var error);

            Assert.That(table, Is.Null);
            Assert.That(error, Does.Contain("multi.mul"));
        }

        // Дом 1: первая часть с флагами 0 входит в границы, третья (флаги 0) — нет. Ожидания посчитаны по
        // MultiComponentList: Min (−3, 0), Max (2, 6), центр (3, 0), размер 6×7. Дом 2: все сдвиги положительные —
        // границы всё равно начинаются с (0, 0): Max (8, 7), размер 9×8.
        private static MultiFileBuilder Example()
        {
            var file = new MultiFileBuilder();
            file.Add(1,
                new Part(0x64, 0, 0, 0, 0, uopFlagCode: 0),
                new Part(0x65, -3, 2, 5, (ulong)TileFlags.Background, uopFlagCode: 0),
                new Part(0x66, 4, -1, 0, 0, uopFlagCode: 1),
                new Part(0x67, 2, 6, -10, (ulong)TileFlags.Generic, uopFlagCode: 257));
            file.Add(2,
                new Part(0x10, 5, 5, 0, (ulong)TileFlags.Background),
                new Part(0x11, 8, 7, 0, (ulong)TileFlags.Background));
            return file;
        }

        private static void AssertExampleHouse(MultiComponents house)
        {
            Assert.That(house.Components.Count, Is.EqualTo(4));
            var part = house.Components[3];
            Assert.That(part.ItemId, Is.EqualTo(0x67));
            Assert.That(part.X, Is.EqualTo(2));
            Assert.That(part.Y, Is.EqualTo(6));
            Assert.That(part.Z, Is.EqualTo(-10));
            Assert.That(part.Flags, Is.EqualTo(TileFlags.Generic));
            Assert.That(house.Components[2].X, Is.EqualTo(4));
            Assert.That((house.MinX, house.MinY, house.MaxX, house.MaxY), Is.EqualTo((-3, 0, 2, 6)));
            Assert.That((house.CenterX, house.CenterY, house.Width, house.Height), Is.EqualTo((3, 0, 6, 7)));
        }

        private static void AssertPositiveHouse(MultiComponents house)
        {
            Assert.That((house.MinX, house.MinY, house.MaxX, house.MaxY), Is.EqualTo((0, 0, 8, 7)));
            Assert.That((house.Width, house.Height), Is.EqualTo((9, 8)));
        }

        private IMultiData LoadMul(MultiFileBuilder file, bool postHs, int count)
        {
            file.BuildMul(postHs, count, out var index, out var data);
            return Load(index, data, postHs);
        }

        private IMultiData Load(byte[] index, byte[] data, bool postHs)
        {
            var files = MultiFiles.Mul(_folder.Bytes("multi.idx", index), _folder.Bytes("multi.mul", data), postHs);
            var table = MultiTable.Load(files, out var error);
            Assert.That(error, Is.Empty);
            return table!;
        }

        private IMultiData LoadUop(byte[] bytes)
        {
            var table = MultiTable.Load(MultiFiles.Uop(_folder.Bytes("MultiCollection.uop", bytes)), out var error);
            Assert.That(error, Is.Empty);
            return table!;
        }

        private string LoadUopError(byte[] bytes)
        {
            var table = MultiTable.Load(MultiFiles.Uop(_folder.Bytes("MultiCollection.uop", bytes)), out var error);
            Assert.That(table, Is.Null);
            return error;
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            for (var i = 0; i + needle.Length <= haystack.Length; i++)
            {
                if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
                {
                    return i;
                }
            }

            throw new InvalidOperationException("кусок не найден");
        }
    }
}
