using System;
using System.Linq;
using NUnit.Framework;
using TheVerge.Network.IO;
using TheVerge.Network.Session;
using TheVerge.Tests.Network.Framing;

namespace TheVerge.Tests.Network.Session
{
    /// <summary>
    /// Разбор пакетов по номеру (PLAN Стадия 1, п. 4а; ADR 0013 п. 7–8). Обработчик получает читатель, стоящий после
    /// заголовка: после номера; у пакета переменной длины — после длины; у подкоманды — после неё (как у сервера:
    /// IncomingExtendedCommandPackets.cs, ExtendedCommand читает подкоманду, обработчик — остальное). Пакет без
    /// обработчика — запись в лог (один раз на номер) и пропуск; пакет короче, чем ждёт обработчик, — лог с дампом
    /// и пропуск (AGENTS.md §3.5).
    /// </summary>
    public sealed class PacketDispatcherTests
    {
        [Test]
        public void FixedPacket_ReaderStartsAfterId()
        {
            var dispatcher = new PacketDispatcher(new RecordingLog());
            uint serial = 0;
            var remaining = -1;
            dispatcher.Register(0x1B, (ref PacketReader reader) =>
            {
                serial = reader.ReadUInt32();
                remaining = reader.Remaining;
            });

            var result = dispatcher.Dispatch(ServerPacketSamples.LoginConfirm());

            Assert.That(result, Is.EqualTo(DispatchResult.Handled));
            Assert.That(serial, Is.EqualTo(0x42));
            Assert.That(remaining, Is.EqualTo(32));
        }

        [Test]
        public void VariablePacket_ReaderStartsAfterLength()
        {
            var dispatcher = new PacketDispatcher(new RecordingLog());
            byte first = 0;
            var remaining = -1;
            dispatcher.Register(0xA8, (ref PacketReader reader) =>
            {
                remaining = reader.Remaining;
                first = reader.ReadByte();
            });

            dispatcher.Dispatch(ServerPacketSamples.ServerList());

            Assert.That(first, Is.EqualTo(0x5D));
            Assert.That(remaining, Is.EqualTo(43));
        }

        [Test]
        public void ExtendedSubCommand_ReaderStartsAfterSubCommand()
        {
            var dispatcher = new PacketDispatcher(new RecordingLog());
            var remaining = -1;
            dispatcher.Register(0xBF, 0x08, (ref PacketReader reader) => remaining = reader.Remaining);

            var result = dispatcher.Dispatch(ServerPacketSamples.MapChange());

            Assert.That(result, Is.EqualTo(DispatchResult.Handled));
            Assert.That(remaining, Is.EqualTo(1));
        }

        [Test]
        public void AssistantSubCommand_ReaderStartsAfterOneByteSubCommand()
        {
            var dispatcher = new PacketDispatcher(new RecordingLog());
            uint serial = 0;
            dispatcher.Register(0xF0, 0x01, (ref PacketReader reader) => serial = reader.ReadUInt32());

            dispatcher.Dispatch(ServerPacketSamples.PartyLocations());

            Assert.That(serial, Is.EqualTo(0x43));
        }

        [Test]
        public void NoHandler_SkippedAndLoggedOncePerPacket()
        {
            var log = new RecordingLog();
            var dispatcher = new PacketDispatcher(log);

            for (var i = 0; i < 3; i++)
            {
                Assert.That(dispatcher.Dispatch(ServerPacketSamples.GlobalLight()), Is.EqualTo(DispatchResult.NoHandler));
            }

            Assert.That(log.Entries.Count(e => e.Message.Contains("0x4F") && e.Message.Contains("GlobalLight")), Is.EqualTo(1));
            Assert.That(log.Errors, Is.Empty);
            Assert.That(log.Warnings, Is.Empty);
        }

        [Test]
        public void OtherSubCommandRegistered_StillNoHandler()
        {
            var called = false;
            var dispatcher = new PacketDispatcher(new RecordingLog());
            dispatcher.Register(0xBF, 0x06, (ref PacketReader reader) => called = true);

            Assert.That(dispatcher.Dispatch(ServerPacketSamples.MapChange()), Is.EqualTo(DispatchResult.NoHandler));
            Assert.That(called, Is.False);
        }

        [Test]
        public void HandlerReadsPastEnd_ReadErrorLoggedWithDump()
        {
            var log = new RecordingLog();
            var dispatcher = new PacketDispatcher(log);
            dispatcher.Register(0x73, (ref PacketReader reader) => reader.ReadUInt16());

            var result = dispatcher.Dispatch(ServerPacketSamples.PingAck());

            Assert.That(result, Is.EqualTo(DispatchResult.ReadError));
            var warning = log.Warnings.Single();
            Assert.That(warning, Does.Contain("0x73").And.Contain("PingAck").And.Contain("73 07"));
        }

        [Test]
        public void Register_PacketServerNeverSends_Throws()
        {
            var dispatcher = new PacketDispatcher(new RecordingLog());

            Assert.Throws<ArgumentException>(new Action(() => dispatcher.Register(0x02, (ref PacketReader r) => { })));
            Assert.Throws<ArgumentException>(new Action(() => dispatcher.Register(0xBF, 0x0001, (ref PacketReader r) => { })));
        }

        [Test]
        public void Register_WrongSubCommandUse_Throws()
        {
            var dispatcher = new PacketDispatcher(new RecordingLog());

            Assert.Throws<ArgumentException>(new Action(() => dispatcher.Register(0xBF, (ref PacketReader r) => { })));
            Assert.Throws<ArgumentException>(new Action(() => dispatcher.Register(0x55, 0x01, (ref PacketReader r) => { })));
        }

        [Test]
        public void Register_Twice_Throws()
        {
            var dispatcher = new PacketDispatcher(new RecordingLog());
            dispatcher.Register(0x55, (ref PacketReader r) => { });
            dispatcher.Register(0xBF, 0x08, (ref PacketReader r) => { });

            Assert.Throws<ArgumentException>(new Action(() => dispatcher.Register(0x55, (ref PacketReader r) => { })));
            Assert.Throws<ArgumentException>(new Action(() => dispatcher.Register(0xBF, 0x08, (ref PacketReader r) => { })));
        }
    }
}
