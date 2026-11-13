using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Bot.Replay;
using TheVerge.Core.Items;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Framing;
using TheVerge.Network.IO;
using TheVerge.Network.Packets.Items;
using TheVerge.Network.Packets.World;
using TheVerge.Network.Session;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Мир из записанного входа на настоящий ModernUO совпадает с дампом сервера без запущенного сервера (PLAN Стадия 2,
    /// п. 11, проверка 4). Запись — Bot/Replays/world-dump-modernuo.tsv: копия сервера с пустым миром 2026-11-13, бот-GM
    /// (пароль в записи скрыт) открыл рюкзак и сказал [DumpVisible; дамп сервера в тот момент — world-dump-modernuo.json.
    /// После дампа в пустом мире ничего не менялось, поэтому мир в конце записи — тот же.
    /// </summary>
    public sealed class RecordedWorldTests
    {
        private static readonly string Replays = Path.Combine(AppContext.BaseDirectory, "Bot", "Replays");

        [Test]
        public void RecordedSession_WorldMatchesServerDump()
        {
            var store = new EntityStore();
            var dispatcher = new PacketDispatcher(new RecordingLog());
            WorldHandlers.Register(dispatcher, store, new WorldEnvironment());
            ItemHandlers.Register(dispatcher, new ContainerContents(store));
            var script = ReplayScript.Read(new StringReader(File.ReadAllText(Path.Combine(Replays, "world-dump-modernuo.tsv"))));

            foreach (var entry in script.Connections[1].Where(IsServerPacket))
            {
                if (entry.Packet[0] == 0x1B)
                {
                    store.Reset(LoginConfirm(entry.Packet));
                    continue;
                }

                if (PacketTable.Get(PacketDirection.ServerToClient, entry.Packet[0]).IsKnown)
                {
                    dispatcher.Dispatch(entry.Packet);
                }
            }

            var server = WorldSnapshot.Read(File.ReadAllText(Path.Combine(Replays, "world-dump-modernuo.json")));
            Assert.That(server.Objects.Count, Is.GreaterThan(3), "в дампе персонаж, надетое и рюкзак с вещами");
            Assert.That(WorldSnapshot.Compare(WorldSnapshot.FromStore(store), server), Is.Empty);
        }

        private static bool IsServerPacket(PacketLogEntry entry) =>
            entry.Kind == PacketLogKind.Packet && entry.Direction == PacketDirection.ServerToClient;

        // 0x1B, как его читает LoginConfirmHandler (OutgoingAccountPackets.cs, SendLoginConfirmation).
        private static PlayerState LoginConfirm(byte[] packet)
        {
            var reader = new PacketReader(packet.AsSpan(1));
            var serial = new Serial(reader.ReadUInt32());
            reader.Skip(4);
            var body = reader.ReadUInt16();
            var position = new Point3D(reader.ReadInt16(), reader.ReadInt16(), (sbyte)reader.ReadInt16());
            var direction = (Direction)reader.ReadByte();
            return new PlayerState(serial, body, position, direction, new MapSize(7168, 4096));
        }
    }
}
