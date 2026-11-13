using System;
using System.Buffers.Binary;
using System.Diagnostics;
using NUnit.Framework;
using TheVerge.Core.Session;
using TheVerge.Network.Framing;
using TheVerge.Network.Packets.Account;
using TheVerge.Network.Packets.Session;
using TheVerge.Network.Session;
using TheVerge.Tests.Core.Session;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Network.Fuzz
{
    /// <summary>
    /// Устойчивость (PLAN Стадия 1, п. 14; критерий «Фаззинг случайными байтами: 1 млн входов без падения и зависания»).
    /// Случайные данные не должны ронять клиент: пакет известной длины, который нельзя разобрать, — лог и пропуск
    /// (PacketDispatcher, DispatchResult.ReadError) или ошибка протокола входа; сломанный поток — ошибка нарезки с дампом
    /// (AGENTS.md §3.5, ADR 0013). Seed — в сообщении: упавший случай повторяется.
    /// </summary>
    public sealed class FuzzTests
    {
        private const int Inputs = 1_000_000;

        private static readonly LoginState[] States =
        {
            LoginState.Idle, LoginState.AwaitingShardList, LoginState.ShardSelection, LoginState.AwaitingRedirect,
            LoginState.AwaitingCharacterList, LoginState.CharacterSelection, LoginState.EnteringWorld,
            LoginState.WorldLoading, LoginState.InWorld, LoginState.LoggingOut,
        };

        [Test]
        public void MillionRandomPackets_ThroughAllHandlers_NoCrash()
        {
            var seed = Environment.TickCount;
            var random = new Random(seed);
            var (flow, dispatcher) = NewClient();
            var buffer = new byte[1024];
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < Inputs; i++)
            {
                if (i % 1000 == 0)
                {
                    flow.Progress.Reset();
                    flow.Progress.MoveTo(States[random.Next(States.Length)]);
                }

                var length = RandomPacket(random, buffer);
                try
                {
                    dispatcher.Dispatch(buffer.AsSpan(0, length));
                }
                catch (Exception e)
                {
                    Assert.Fail("seed " + seed + ", вход " + i + ": " + BitConverter.ToString(buffer, 0, length) + " → " + e);
                }
            }

            Assert.That(watch.Elapsed.TotalSeconds, Is.LessThan(60), "seed " + seed);
        }

        [Test]
        public void MillionRandomChunks_IntoFramer_PacketsOrErrorWithDump_NeverStuck()
        {
            var seed = Environment.TickCount;
            var random = new Random(seed);
            var (_, dispatcher) = NewClient();
            var framer = new PacketFramer(PacketDirection.ServerToClient);
            var chunk = new byte[64];
            for (var i = 0; i < Inputs; i++)
            {
                var size = random.Next(1, chunk.Length + 1);
                random.NextBytes(chunk.AsSpan(0, size));
                try
                {
                    framer = Feed(framer, chunk.AsSpan(0, size), dispatcher);
                }
                catch (Exception e)
                {
                    Assert.Fail("seed " + seed + ", кусок " + i + ": " + e);
                }
            }
        }

        // Нарезка принимает всё (ни один кусок не застревает навсегда) и отдаёт пакеты или ошибку с коротким дампом;
        // после ошибки соединение рвётся — здесь начинается новая нарезка, как у нового соединения.
        private static PacketFramer Feed(PacketFramer framer, ReadOnlySpan<byte> data, PacketDispatcher dispatcher)
        {
            while (!data.IsEmpty)
            {
                var accepted = framer.Append(data);
                data = data.Slice(accepted);
                FrameStatus status;
                while ((status = framer.Next(out var packet)) == FrameStatus.Packet)
                {
                    dispatcher.Dispatch(packet);
                }

                if (status == FrameStatus.Error)
                {
                    Assert.That(framer.Error.Dump.Length, Is.InRange(1, Math.Max(ClientLimits.ErrorDumpBytes, PacketInfo.MaxLength)));
                    framer = new PacketFramer(PacketDirection.ServerToClient);
                }
                else if (accepted == 0)
                {
                    Assert.Fail("нарезка не принимает данные и не отдаёт пакет — зависла");
                }
            }

            return framer;
        }

        // Случайный пакет, который прошёл бы нарезку: номер известный (чаще) или любой, длина по таблице или случайная.
        private static int RandomPacket(Random random, byte[] buffer)
        {
            var id = (byte)random.Next(256);
            var info = PacketTable.Get(PacketDirection.ServerToClient, id);
            if (!info.IsKnown)
            {
                return RandomPacket(random, buffer);
            }

            // Dispatch получает только то, что пропустила нарезка: у пакета с подкомандой длина не меньше её конца.
            var layout = PacketTable.GetSubCommandLayout(PacketDirection.ServerToClient, id);
            var minLength = layout.HasSubCommand ? layout.MinPacketLength : PacketInfo.VariableHeaderLength;
            var length = info.IsVariable ? random.Next(minLength, buffer.Length) : info.FixedLength;
            random.NextBytes(buffer.AsSpan(0, length));
            buffer[0] = id;
            if (info.IsVariable)
            {
                BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(PacketInfo.LengthOffset), (ushort)length);
                FixSubCommand(random, buffer, id, length);
            }

            return length;
        }

        // Подкоманда — из существующих: неизвестную режет нарезка (проверено в 1.2), до обработчиков она не доходит.
        private static void FixSubCommand(Random random, byte[] buffer, byte id, int length)
        {
            var layout = PacketTable.GetSubCommandLayout(PacketDirection.ServerToClient, id);
            if (!layout.HasSubCommand)
            {
                return;
            }

            while (!PacketTable.Get(PacketDirection.ServerToClient, id, layout.Read(buffer)).IsKnown)
            {
                for (var i = layout.MinPacketLength - layout.Size; i < layout.MinPacketLength; i++)
                {
                    buffer[i] = (byte)(layout.Size == 2 && i == layout.MinPacketLength - 2 ? 0 : random.Next(256));
                }
            }
        }

        private static (LoginFlow, PacketDispatcher) NewClient()
        {
            var connector = new RecordingConnector();
            var flow = new LoginFlow(new RecordingAccountSender(), connector, 1);
            var sender = new RecordingSessionSender();
            var dispatcher = new PacketDispatcher(new RecordingLog());
            AccountHandlers.Register(dispatcher, flow);
            SessionHandlers.Register(dispatcher, new WorldEntry(flow.Progress, sender), new ServerRequests(sender),
                new GameSession(flow.Progress, sender, connector, new FakeClock()));
            return (flow, dispatcher);
        }
    }
}
